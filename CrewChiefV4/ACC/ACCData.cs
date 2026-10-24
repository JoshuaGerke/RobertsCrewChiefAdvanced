using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CrewChiefV4.GameState;

namespace CrewChiefV4.ACC
{
    class accConstant
    {
        public const string SharedMemoryNamePhysics = "Local\\acpmf_physics"; // Local\\acpmf_physics
        public const string SharedMemoryNameGraphic = "Local\\acpmf_graphics"; // Local\\acpmf_graphics
        public const string SharedMemoryNameStatic = "Local\\acpmf_static"; // Local\\acpmf_static
        public const string SharedMemoryNameCrewChief = "Local\\acpmf_static"; // Local\\acpmf_static

        public const string SettingMachineIpAddress = "acc_machine_ip";
    }

    namespace accData
    {
        public enum AC_STATUS
        {
            AC_OFF = 0,
            AC_REPLAY = 1,
            AC_LIVE = 2,
            AC_PAUSE = 3
        }

        public enum AC_SESSION_TYPE
        {
            AC_UNKNOWN = -1,
            AC_PRACTICE = 0,
            AC_QUALIFY = 1,
            AC_RACE = 2,
            AC_HOTLAP = 3,
            AC_TIME_ATTACK = 4,
            AC_DRIFT = 5,
            AC_DRAG = 6,
            ACC_HOTSTINT = 7,
            ACC_HOTSTINTSUPERPOLE = 8
        }

        public enum AC_FLAG_TYPE
        {
            AC_NO_FLAG = 0,
            AC_BLUE_FLAG = 1,
            AC_YELLOW_FLAG = 2,
            AC_BLACK_FLAG = 3,
            AC_WHITE_FLAG = 4,
            AC_CHECKERED_FLAG = 5,
            AC_PENALTY_FLAG = 6,
        }
        public enum AC_WHEELS
        {
            FL = 0,
            FR = 1,
            RL = 2,
            RR = 3,
        }
        public enum AC_PENALTY_TYPE
        {
            ACC_None = 0,
            ACC_DriveThrough_Cutting = 1,
            ACC_StopAndGo_10_Cutting = 2,
            ACC_StopAndGo_20_Cutting = 3,
            ACC_StopAndGo_30_Cutting = 4,
            ACC_Disqualified_Cutting = 5,
            ACC_RemoveBestLaptime_Cutting = 6,
            ACC_DriveThrough_PitSpeeding = 7,
            ACC_StopAndGo_10_PitSpeeding = 8,
            ACC_StopAndGo_20_PitSpeeding = 9,
            ACC_StopAndGo_30_PitSpeeding = 10,
            ACC_Disqualified_PitSpeeding = 11,
            ACC_RemoveBestLaptime_PitSpeeding = 12,
            ACC_Disqualified_IgnoredMandatoryPit = 13,
            ACC_PostRaceTime = 14,
            ACC_Disqualified_Trolling = 15,
            ACC_Disqualified_PitEntry = 16,
            ACC_Disqualified_PitExit = 17,
            ACC_Disqualified_Wrongway = 18,
            ACC_DriveThrough_IgnoredDriverStint = 19,
            ACC_Disqualified_IgnoredDriverStint = 20,
            ACC_Disqualified_ExceededDriverStintLimit = 21,
            ACC_DriveThrough_False_Start = 30    /* this one isn't documented, but it's what i got for a false start */
        }

        public enum ACC_TRACK_GRIP_STATUS
        {
            ACC_GREEN = 0,
            ACC_FAST = 1,
            ACC_OPTIMUM = 2,
            ACC_GREASY = 3,
            ACC_DAMP = 4,
            ACC_WET = 5,
            ACC_FLOODED = 6
        }

        public enum ACC_RAIN_INTENSITY
        {
            ACC_NO_RAIN = 0,
            ACC_DRIZZLE = 1,
            ACC_LIGHT_RAIN = 2,
            ACC_MEDIUM_RAIN = 3,
            ACC_HEAVY_RAIN = 4,
            ACC_THUNDERSTORM = 5
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        [Serializable]
        public struct SPageFilePhysics
        {
            public int packetId;
            public float gas;
            public float brake;
            public float fuel;
            public int gear;
            public int rpms;
            public float steerAngle;
            public float speedKmh;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] velocity;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] accG;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] wheelSlip;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_wheelLoad;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] wheelsPressure;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] wheelAngularSpeed;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_tyreWear;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_tyreDirtyLevel;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreCoreTemperature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_camberRAD;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] suspensionTravel;

            public float NOT_SET_drs;
            public float tc;
            public float heading;
            public float pitch;
            public float roll;
            public float NOT_SET_cgHeight;
            // this is a weird one, it's documented as just "damage", front, rear, left, right, centre. Centre is, apparently, the sum of the other 4. No idea on scale
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public float[] carDamage;
            public int NOT_SET_numberOfTyresOut;
            public int pitLimiterOn;
            public float abs;
            public float NOT_SET_kersCharge;
            public float NOT_SET_kersInput;
            public int autoShifterOn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public float[] NOT_SET_rideHeight;
            public float turboBoost;
            public float NOT_SET_ballast;
            public float NOT_SET_airDensity;
            public float airTemp;
            public float roadTemp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] localAngularVel;
            public float finalFF;
            public float NOT_SET_performanceMeter;

            public int NOT_SET_engineBrake;
            public int NOT_SET_ersRecoveryLevel;
            public int NOT_SET_ersPowerLevel;
            public int NOT_SET_ersHeatCharging;
            public int NOT_SET_ersIsCharging;
            public float NOT_SET_kersCurrentKJ;

            public int NOT_SET_drsAvailable;
            public int NOT_SET_drsEnabled;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] brakeTemp;
            public float clutch;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_tyreTempI;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_tyreTempM;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_tyreTempO;
            public int isAIControlled;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public accVec3[] tyreContactPoint;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public accVec3[] tyreContactNormal;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public accVec3[] tyreContactHeading;
            float brakeBias;
            public accVec3 localVelocity;

            public int NOT_SET_P2PActivation; // Not used in ACC
            public int NOT_SET_P2PStatus;     // Not used in ACC
            public float NOT_SET_currentMaxRpm;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_mz;       // Not shown in ACC
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_fx;       // Not shown in ACC
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_fy;       // Not shown in ACC
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] slipRatio;// Tyre slip ratio[FL, FR, RL, RR]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] slipAngle;// Tyre slip angle[FL, FR, RL, RR]
            public int NOT_SET_tcinAction;
            public int NOT_SET_absInAction;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            // i think this is set (lf, rf, lr, rr) - not sure on unit, 0 - 1?
            public float[] NOT_SET_suspensionDamage;// Suspensions damage levels[FL, FR, RL, RR]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreTemp;// * Tyres core temperatures[FL, FR, RL, RR] - this is actually set (despite being listed as unset in the documentation)
                                    // and is a copy of the tyreCoreTemperature array (why???)
            public float waterTemp;

            //some unmapped fields at the end of this block:
            /*public float[] brakePressure; // Brake pressure [FL, FR, RL, RR]
            public int frontBrakeCompound; // Brake pad compund front
            public int rearBrakeCompound; // Brake pad compund rear
            public float[] padLife; //   Brake pad wear  [FL, FR, RL, RR]
            public float[] discLife; //  Brake disk wear  [FL, FR, RL, RR]
            public int ignitionOn; // Ignition switch set to on?
            public int starterEngineOn; // Starter Switch set to on?
            public int isEngineRunning; // Engine running?
            public float kerbVibration; // vibrations sent to the FFB, could be used for motion rigs
            public float slipVibrations; // vibrations sent to the FFB, could be used for motion rigs
            public float gVibrations; // vibrations sent to the FFB, could be used for motion rigs
            public float absVibrations; // vibrations sent to the FFB, could be used for motion rigs*/
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        [Serializable]
        public struct SPageFileGraphic
        {
            public int packetId;
            public AC_STATUS status;
            public AC_SESSION_TYPE session;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String currentTime;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String lastTime;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String bestTime;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String split;
            public int completedLaps;
            public int position;
            public int iCurrentTime;
            public int iLastTime;
            public int iBestTime;
            public float sessionTimeLeft;
            public float distanceTraveled;
            public int isInPit;
            public int currentSectorIndex;
            public int lastSectorTime;
            public int numberOfLaps;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String tyreCompound;

            public float NOT_SET_replayTimeMultiplier;
            public float normalizedCarPosition;

            // note that carCount frequently disagrees with the UDP data - looks like UDP data is more correct
            public int carCount;
            
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
            public accVec3[] carCoordinates;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
            public int[] carIDs;
            public int playerCarID;

            public float penaltyTime;
            public AC_FLAG_TYPE flag;
            
            public AC_PENALTY_TYPE penalty;

            public int idealLineOn;
            public int isInPitLane;

            public float NOT_SET_surfaceGrip;
            public int MandatoryPitDone;
            public float windSpeed;
            public float windDirection;

            public int isSetupMenuVisible;

            public int mainDisplayIndex;
            public int secondaryDisplayIndex;
            public int TC;
            public int TCCut;
            public int EngineMap;
            public int ABS;
            public int fuelXLap;
            public int rainLights;
            public int flashingLights;
            public int lightsStage;
            public float exhaustTemperature;
            public int wiperLV;

            public int driverStintTotalTimeLeft;// Time is the driver is allowed to drive per race in milliseconds
            public int driverStintTimeLeft;// Time the driver is allowed to drive per stint in milliseconds
            public int rainTyres;// Are rain tyres equipped
            public int sessionIndex; // zero-indexed counter of all sessions in weekend, prac1 is zero, increments for each subsequent session, doesn't reset between session types
            public float usedFuel;// Used fuel since last time refueling
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String deltaLapTime;// Delta time in wide character
            public int iDeltaLapTime;//Delta time time in milliseconds
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String estimatedLapTime;//Estimated lap time in milliseconds
            public int iEstimatedLapTime;//Estimated lap time in wide character
            public int isDeltaPositive;//Delta positive(1) or negative(0)
            public int iSplit;// Last split time in milliseconds
            public int isValidLap;// Check if Lap is valid for timing

            // new fields to be tested

            public float fuelEstimatedLaps; //  Laps possible with current fuel level

            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string trackStatus; // (Green, Fast, Optimum, Damp, Wet)
            public int missingMandatoryPits; // Mandatory pitstops the player still has to do, may be 255 in sessions with no mandatory stop, 
                                             // probably zero when the stops have been completed
            public float Clock; // Time of day in seconds
            public int directionLightsLeft; // Is Blinker left on (WTF? There's no usable race position but we have blinker status?)
            public int directionLightsRight; // Is Blinker right on
            public int GlobalYellow;  // 0 or 1? Not documented
            public int GlobalYellow1; // Yellow Flag in Sector 1 is out?
            public int GlobalYellow2; // Yellow Flag in Sector 2 is out?
            public int GlobalYellow3; // Yellow Flag in Sector 3 is out?
            public int GlobalWhite; //  White Flag is out?
            public int GlobalGreen; //  Green Flag is out?
            public int GlobalChequered; //  Checkered Flag is out?
            public int GlobalRed; //  Red Flag is out?
            public int mfdTyreSet; //  # of tyre set on the MFD. THIS STARTS AT 0 IS AS MFD / PIT MENU SET - 1
            public float mfdFuelToAdd; //  How much fuel to add on the MFD

            // note these are the tyre pressure you've set in the MFD (for pit changes), *not* the current pressure
            public float mfdTyrePressureLF; //  Tyre pressure left front on the MFD
            public float mfdTyrePressureRF; //  Tyre pressure right front on the MFD
            public float mfdTyrePressureLR; //  Tyre pressure left rear on the MFD
            public float mfdTyrePressureRR; //  Tyre pressure right rear on the MFD
            public ACC_TRACK_GRIP_STATUS trackGripStatus;
            public ACC_RAIN_INTENSITY rainIntensity;
            public ACC_RAIN_INTENSITY rainIntensityIn10min;
            public ACC_RAIN_INTENSITY rainIntensityIn30min;

            public int currentTyreSet;  // THIS STARTS AT 1 AND IS THE SAME DIGIT AS DISPLAYED IN THE PIT MENU
            public int strategyTyreSet;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        [Serializable]
        public struct SPageFileStatic
        {
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String smVersion;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public String acVersion;

            // session static info
            public int numberOfSessions;
            public int numCars;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String carModel;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String track;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String playerName;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String playerSurname;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String playerNick;
            public int sectorCount;

            // car static info
            public float NOT_SET_maxTorque;
            public float NOT_SET_maxPower;
            public int maxRpm;
            public float maxFuel;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_suspensionMaxTravel;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] NOT_SET_tyreRadius;
            public float NOT_SET_maxTurboBoost;

            public float NOT_SET_deprecated_1;
            public float NOT_SET_deprecated_2;

            public int penaltiesEnabled;

            public float aidFuelRate;
            public float aidTireRate;
            public float aidMechanicalDamage;
            public int aidAllowTyreBlankets;
            public float aidStability;
            public int aidAutoClutch;
            public int aidAutoBlip;

            public int NOT_SET_hasDRS;
            public int NOT_SET_hasERS;
            public int NOT_SET_hasKERS;
            public float NOT_SET_kersMaxJ;
            public int NOT_SET_engineBrakeSettingsCount;
            public int NOT_SET_ersPowerControllerCount;

            public float NOT_SET_trackSPlineLength;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String NOT_SET_trackConfiguration;   // this is always "track config" for some unfathomable reason
            public float NOT_SET_ersMaxJ;
            public int SET_FROM_UDP_isTimedRace;
            public int NOT_SET_hasExtraLap;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public String NOT_SET_carSkin;
            public int NOT_SET_reversedGridPositions;
            public int PitWindowStart;
            public int PitWindowEnd;
            public int isOnline; // If is a multiplayer session

            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string dryTyresName; // Name of the dry tyres

            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string wetTyresName; // Name of the wet tyres
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct accVec3
        {
            public float x;
            public float y;
            public float z;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct accVehicleInfo
        {
            public int carId;
            public int isPlayerVehicle;
            public string driverName;
            public string carModel;
            public float speedMS; // note that the data in this struct aren't updated on every tick. Use speedKmh from Physics for the player speed
            public int bestLapMS;
            public int bestSplit1TimeMS;
            public int bestSplit2TimeMS;
            public int bestSplit3TimeMS;
            public int lapCount;
            public int currentLapInvalid;
            public int currentLapTimeMS;
            public int lastLapTimeMS;
            public int lastSplit1TimeMS;
            public int lastSplit2TimeMS;
            public int lastSplit3TimeMS;
            public accVec3 worldPosition;
            public int isCarInPitlane;  // car is inside the pit limiter region
            public int isCarInPitEntry; // car is within the pit entry region
            public int isCarInPitExit;  // car is within the pit exit region
            public int carLeaderboardPosition;
            public int carRealTimeLeaderboardPosition; // for race sessions this is derived from the total distance travelled, for other sessions it's the carLeaderboardPosition
            public float spLineLength;
            public int isConnected; // NOT USED, IS ALWAYS 1
            public int raceNumber;
        }

        public class SPageFileCrewChief
        {
            public int focusVehicle;
            public accVehicleInfo[] vehicle = new accVehicleInfo[0];
            public byte[] acInstallPath;
            public int isInternalMemoryModuleLoaded;
            public byte[] pluginVersion;
            public SessionPhase SessionPhase;
            public float trackLength;
            public float rainLevel;
        }

        public class ACCShared
        {
            public SPageFilePhysics accPhysics;
            public SPageFileGraphic accGraphic;
            public SPageFileStatic accStatic;
            public SPageFileCrewChief accChief;
        }
    }
}
 