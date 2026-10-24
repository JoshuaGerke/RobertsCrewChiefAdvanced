using System;
using System.Runtime.InteropServices;

namespace CrewChiefV4.ACE
{
    #region Enums
    class aceConstant
    {
        public const string SharedMemoryNamePhysics = "Local\\acevo_pmf_physics";
        public const string SharedMemoryNameGraphic = "Local\\acevo_pmf_graphics";
        public const string SharedMemoryNameStatic = "Local\\acevo_pmf_static";
        public const string SettingMachineIpAddress = "ace_machine_ip";
        public const int MaxVehicles = 64;
        public const int MaxOpponents = 60;
        public const int MM_PHYSICS_STRUCT_SIZE = 800;
        public const int MM_GRAPHICS_STRUCT_SIZE = 4900;
        public const int MM_STATIC_STRUCT_SIZE = 208;
    }

    namespace aceData
    {
        public enum ACEVO_STATUS
        {
            AC_OFF = 0,                      // Simulator is not running / no session active
            AC_REPLAY = 1,                   // A replay is currently being played back
            AC_LIVE = 2,                     // Live driving session is active
            AC_PAUSE = 3,                    // Session is paused
        }

        public enum ACEVO_SESSION_TYPE
        {
            AC_UNKNOWN = -1,                 // Session type not yet determined
            AC_TIME_ATTACK = 0,              // Time attack / qualifying session
            AC_RACE = 1,                     // Race session
            AC_HOT_STINT = 2,                // Hot-stint practice
            AC_CRUISE = 3,                   // Untimed cruise
        }

        public enum ACEVO_FLAG_TYPE
        {
            AC_NO_FLAG = 0,                  // No flag displayed
            AC_WHITE_FLAG = 1,               // Slow vehicle ahead on track
            AC_GREEN_FLAG = 2,               // Track clear — racing resumed
            AC_RED_FLAG = 3,                 // Session stopped due to incident or hazard
            AC_BLUE_FLAG = 4,                // Lapped car must yield to the race leader
            AC_YELLOW_FLAG = 5,              // Hazard present — no overtaking
            AC_BLACK_FLAG = 6,               // Driver disqualified / must pit immediately
            AC_BLACK_WHITE_FLAG = 7,         // Warning for unsportsmanlike behaviour
            AC_CHECKERED_FLAG = 8,           // Session or race has ended
            AC_ORANGE_CIRCLE_FLAG = 9,       // Mechanical problem — car must pit
            AC_RED_YELLOW_STRIPES_FLAG = 10, // Slippery surface ahead on track
        }

        public enum ACEVO_CAR_LOCATION
        {
            ACEVO_UNASSIGNED = 0,            // Position not yet determined
            ACEVO_PITLANE = 1,               // Car is inside the pit lane
            ACEVO_PITENTRY = 2,              // Car is at the pit-lane entry
            ACEVO_PITEXIT = 3,               // Car is at the pit-lane exit
            ACEVO_TRACK = 4,                 // Car is on the racing circuit
        }

        public enum ACEVO_ENGINE_TYPE
        {
            ACEVO_INTERNAL_COMBUSTION = 0,   // Traditional petrol/diesel internal combustion engine
            ACEVO_ELECTRIC_MOTOR = 1,        // Fully electric powertrain
        }

        public enum ACEVO_STARTING_GRIP
        {
            ACEVO_GREEN = 0,                 // Track grip at minimum
            ACEVO_FAST = 1,                  // Track grip in advanced = fast, // stage
            ACEVO_OPTIMUM = 2,               // Track conditions starting at optimum grip
        }

        public enum AC_WHEELS
        {
            FL = 0,
            FR = 1,
            RL = 2,
            RR = 3,
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
            public float[] wheelLoad;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] wheelsPressure;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] wheelAngularSpeed;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreWear;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreDirtyLevel;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreCoreTemperature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] camberRAD;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] suspensionTravel;

            public float drs;
            public float tc;
            public float heading;
            public float pitch;
            public float roll;
            public float cgHeight;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public float[] carDamage;
            public int numberOfTyresOut;
            public int pitLimiterOn;
            public float abs;
            public float kersCharge;
            public float kersInput;
            public int autoShifterOn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public float[] rideHeight;
            public float turboBoost;
            public float ballast;
            public float airDensity;
            public float airTemp;
            public float roadTemp;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] localAngularVel;
            public float finalFF;
            public float performanceMeter;

            public int engineBrake;
            public int ersRecoveryLevel;
            public int ersPowerLevel;
            public int ersHeatCharging;
            public int ersIsCharging;
            public float kersCurrentKJ;

            public int drsAvailable;
            public int drsEnabled;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] brakeTemp;
            public float clutch;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreTempI;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreTempM;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreTempO;
            public int isAIControlled;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public aceVec3[] tyreContactPoint;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public aceVec3[] tyreContactNormal;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public aceVec3[] tyreContactHeading;
            public float brakeBias;
            public aceVec3 localVelocity;
            public int P2PActivations;                           // Remaining Push-to-Pass activations
            public int P2PStatus;                                // Push-to-Pass status (0 = inactive, 1 = active)
            public int currentMaxRpm;                            // Current rev-limiter ceiling in RPM
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] mz;                                   // Self-aligning tyre torque (Mz) per wheel [FL, FR, RL, RR] in Nm
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] fx;                                   // Longitudinal tyre force (Fx) per wheel [FL, FR, RL, RR] in N
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] fy;                                   // Lateral tyre force (Fy) per wheel [FL, FR, RL, RR] in N
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] slipRatio;                            // Longitudinal slip ratio per tyre [FL, FR, RL, RR]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] slipAngle;                            // Lateral slip angle per tyre in radians [FL, FR, RL, RR]
            public int tcinAction;                               // Traction control currently cutting power (0 = no, 1 = yes)
            public int absInAction;                              // ABS currently modulating brakes (0 = no, 1 = yes)
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] suspensionDamage;                     // Suspension structural damage per corner (0.0–1.0) [FL, FR, RL, RR]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreTemp;                             // Representative tyre surface temperature per wheel in °C [FL, FR, RL, RR]
            public float waterTemp;                              // Engine coolant temperature in °C
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] brakeTorque;                          // Braking torque at each wheel in Nm [FL, FR, RL, RR]
            public int frontBrakeCompound;                       // Front brake-pad compound identifier
            public int rearBrakeCompound;                        // Rear brake-pad compound identifier
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] padLife;                              // Brake-pad remaining life per corner (0.0–1.0) [FL, FR, RL, RR]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] discLife;                             // Brake-disc remaining life per corner (0.0–1.0) [FL, FR, RL, RR]
            public int ignitionOn;                               // Ignition switch state (0 = off, 1 = on)
            public int starterEngineOn;                          // Starter motor currently cranking (0 = no, 1 = yes)
            public int isEngineRunning;                          // Engine is running (0 = stopped, 1 = running)
            public float kerbVibration;                          // Vibration intensity transmitted from kerb strikes
            public float slipVibrations;                         // Vibration intensity caused by tyre slip
            public float roadVibrations;                         // Vibration intensity from road surface texture
            public float absVibrations;                          // Vibration intensity generated by ABS pulsing


        }

        #endregion

        #region Supporting structs
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoTyreState
        {
            public float slip;
            [MarshalAs(UnmanagedType.I1)]
            public bool @lock;
            public float tyre_pression;
            public float tyre_temperature_c;
            public float brake_temperature_c;
            public float brake_pressure;
            public float tyre_temperature_left;
            public float tyre_temperature_center;
            public float tyre_temperature_right;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string tyre_compound_front;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string tyre_compound_rear;

            public float tyre_normalized_pressure;
            public float tyre_normalized_temperature_left;
            public float tyre_normalized_temperature_center;
            public float tyre_normalized_temperature_right;
            public float brake_normalized_temperature;
            public float tyre_normalized_temperature_core;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoDamageState
        {
            public float damage_front;
            public float damage_rear;
            public float damage_left;
            public float damage_right;
            public float damage_center;
            public float damage_suspension_lf;
            public float damage_suspension_rf;
            public float damage_suspension_lr;
            public float damage_suspension_rr;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 92)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoPitInfo
        {
            public sbyte damage;
            public sbyte fuel;
            public sbyte tyres_lf;
            public sbyte tyres_rf;
            public sbyte tyres_lr;
            public sbyte tyres_rr;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 58)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoElectronics
        {
            public sbyte tc_level;
            public sbyte tc_cut_level;
            public sbyte abs_level;
            public sbyte esc_level;
            public sbyte ebb_level;
            public float brake_bias;
            public sbyte engine_map_level;
            public float turbo_level;
            public sbyte ers_deployment_map;
            public float ers_recharge_map;

            [MarshalAs(UnmanagedType.I1)]
            public bool is_ers_heat_charging_on;
            [MarshalAs(UnmanagedType.I1)]
            public bool is_ers_overtake_mode_on;
            [MarshalAs(UnmanagedType.I1)]
            public bool is_drs_open;

            public sbyte diff_power_level;
            public sbyte diff_coast_level;
            public sbyte front_bump_damper_level;
            public sbyte front_rebound_damper_level;
            public sbyte rear_bump_damper_level;
            public sbyte rear_rebound_damper_level;

            [MarshalAs(UnmanagedType.I1)]
            public bool is_ignition_on;
            [MarshalAs(UnmanagedType.I1)]
            public bool is_pitlimiter_on;

            public sbyte active_performance_mode;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 88)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoInstrumentation
        {
            public sbyte main_light_stage;
            public sbyte special_light_stage;
            public sbyte cockpit_light_stage;
            public sbyte wiper_level;

            [MarshalAs(UnmanagedType.I1)]
            public bool rain_lights;
            [MarshalAs(UnmanagedType.I1)]
            public bool direction_light_left;
            [MarshalAs(UnmanagedType.I1)]
            public bool direction_light_right;
            [MarshalAs(UnmanagedType.I1)]
            public bool flashing_lights;
            [MarshalAs(UnmanagedType.I1)]
            public bool warning_lights;

            public sbyte selected_display_index;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public sbyte[] display_current_page_index;

            [MarshalAs(UnmanagedType.I1)]
            public bool are_headlights_visible;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 101)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoSessionState
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string phase_name;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string timeLeft;                              // Formatted remaining session time (HH:MM:SS)
            public int timeLeftMs;                               // Remaining session time in milliseconds
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string waitTime;                              // Formatted wait time before session start
            public int totalLap;                                 // Total laps scheduled for this session
            public int currentLap;                               // Current lap number being driven
            public int lightsOn;                                 // Number of starting lights currently illuminated
            public int lightsMode;                               // Starting-light sequence mode identifier
            public float lapLengthKm;                            // Track lap length in kilometres
            public int endSessionFlag;                           // Non-zero when the session is ending
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string timeToNextSession;                     // Formatted countdown to the next session
            [MarshalAs(UnmanagedType.I1)]
            public bool disconnectedFromServer;                  // Player has lost connection to the game server
            [MarshalAs(UnmanagedType.I1)]
            public bool restartSeasonEnabled;                    // Season restart option is available to the player
            [MarshalAs(UnmanagedType.I1)]
            public bool uiEnableDrive;                           // Drive button is enabled in the UI
            [MarshalAs(UnmanagedType.I1)]
            public bool uiEnableSetup;                           // Setup screen is accessible from the UI
            [MarshalAs(UnmanagedType.I1)]
            public bool isReadyToNextBlinking;                   // Ready-to-proceed indicator is blinking
            [MarshalAs(UnmanagedType.I1)]
            public bool showWaitingForPlayers;                   // Waiting-for-players lobby screen is shown

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 140)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoTimingState
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string current_laptime;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string delta_current;
            public int delta_current_p;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string last_laptime;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string delta_last;
            public int delta_last_p;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string best_laptime;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string ideal_laptime;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string total_time;

            [MarshalAs(UnmanagedType.I1)]
            public bool is_invalid;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 137)]
            public byte[] place_holder;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SMEvoAssistsState
        {
            public byte auto_gear;
            public byte auto_blip;
            public byte auto_clutch;
            public byte auto_clutch_on_start;
            public byte manual_ignition_e_start;
            public byte auto_pit_limiter;
            public byte standing_start_assist;

            public float auto_steer;
            public float arcade_stability_control;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
            public byte[] place_holder;
        }

        #endregion

        #region Graphics

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        public struct carId
        {
            public UInt64 carIdA;
            public UInt64 carIdB;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        [Serializable]
        public struct SPageFileGraphicEvo
        {
            public int packetId;                                 // Incrementing counter — detect new frames by comparing to previous value
            public ACEVO_STATUS status;                          // Current simulator operational state (see ACEVO_STATUS)
            public UInt64 focusedCarIdA;                         // Unique ID of the car currently shown by the camera
            public UInt64 focusedCarIdB;                         // 
            public UInt64 playerCarIdA;                          // Unique ID of the player's own car
            public UInt64 playerCarIdB;                          // 
            public ushort rpm;                                   // Engine speed in RPM for HUD display
            [MarshalAs(UnmanagedType.I1)]
            public bool isRpmLimiterOn;                          // Rev limiter is cutting fuel / ignition (bouncing off limiter)
            [MarshalAs(UnmanagedType.I1)]
            public bool isChangeUpRpm;                           // Engine RPM is in the upshift window
            [MarshalAs(UnmanagedType.I1)]
            public bool isChangeDownRpm;                         // Engine RPM is in the downshift window
            [MarshalAs(UnmanagedType.I1)]
            public bool tcActive;                                // Traction control is actively intervening this frame
            [MarshalAs(UnmanagedType.I1)]
            public bool absActive;                               // ABS is actively modulating brake pressure this frame
            [MarshalAs(UnmanagedType.I1)]
            public bool escActive;                               // Electronic stability control is intervening this frame
            [MarshalAs(UnmanagedType.I1)]
            public bool launchActive;                            // Launch control system is engaged
            [MarshalAs(UnmanagedType.I1)]
            public bool isIgnitionOn;                            // Ignition switch is on
            [MarshalAs(UnmanagedType.I1)]
            public bool isEngineRunning;                         // Engine is running
            [MarshalAs(UnmanagedType.I1)]
            public bool kersIsCharging;                          // KERS/ERS battery is currently being charged
            [MarshalAs(UnmanagedType.I1)]
            public bool isWrongWay;                              // Car is travelling in the wrong direction on track
            [MarshalAs(UnmanagedType.I1)]
            public bool isDrsAvailable;                          // DRS activation is permitted in this section
            [MarshalAs(UnmanagedType.I1)]
            public bool batteryIsCharging;                       // High-voltage battery pack is in charging state
            [MarshalAs(UnmanagedType.I1)]
            public bool isMaxKjPerLapReached;                    // Maximum ERS deployment energy for this lap has been consumed
            [MarshalAs(UnmanagedType.I1)]
            public bool isMaxChargeKjPerLapReached;              // Maximum ERS charge energy for this lap has been stored
            public short displaySpeedKmh;                        // Displayed speed in km/h
            public short displaySpeedMph;                        // Displayed speed in mph
            public short displaySpeedMs;                         // Displayed speed in m/s
            public float pitspeedingDelta;                       // Speed delta vs. pit-lane limit (negative = under limit)
            public short gearInt;                                // Current gear as an integer (same encoding as physics gear)
            public float rpmPercent;                             // Engine RPM as a fraction of redline (0.0–1.0)
            public float gasPercent;                             // Throttle pedal position as a fraction (0.0–1.0)
            public float brakePercent;                           // Brake pressure as a fraction (0.0–1.0)
            public float handbrakePercent;                       // Handbrake engagement as a fraction (0.0–1.0)
            public float clutchPercent;                          // Clutch disengagement as a fraction (1.0–0.0)
            public float steeringPercent;                        // Steering wheel position (-1.0 = full left, +1.0 = full right)
            public float ffbStrength;                            // Global force-feedback output strength
            public float carFfbMupliplier;                       // Per-car force-feedback gain multiplier
            public float waterTemperaturePercent;                // Coolant temperature as a fraction of optimal operating range
            public float waterPressureBar;                       // Coolant system pressure in bar
            public float fuelPressureBar;                        // Fuel system pressure in bar
            public sbyte waterTemperatureC;                      // Coolant temperature in °C
            public sbyte airTemperatureC;                        // Ambient air temperature in °C
            public float oilTemperatureC;                        // Engine oil temperature in °C
            public float oilPressureBar;                         // Engine oil pressure in bar
            public float exhaustTemperatureC;                    // Exhaust gas temperature in °C
            public float gForcesX;                               // Lateral G-force (positive = rightward)
            public float gForcesY;                               // Longitudinal G-force (positive = under acceleration)
            public float gForcesZ;                               // Vertical G-force (positive = upward)
            public float turboBoost;                             // Absolute turbo boost pressure in bar
            public float turboBoostLevel;                        // Current boost stage or map level
            public float turboBoostPerc;                         // Turbo boost as a fraction of maximum (0.0–1.0)
            public int steerDegrees;                             // Steering wheel rotation in degrees from centre
            public float currentKm;                              // Distance driven in the current session in km
            public uint totalKm;                                 // Total odometer / career distance in km
            public uint totalDrivingTimeS;                       // Total driving time accumulated in seconds
            public int timeOfDayHours;                           // In-game time of day — hours (0–23)
            public int timeOfDayMinutes;                         // In-game time of day — minutes (0–59)
            public int timeOfDaySeconds;                         // In-game time of day — seconds (0–59)
            public int deltaTimeMs;                              // Delta vs. reference lap in milliseconds (signed)
            public int currentLapTimeMs;                         // Current lap time in milliseconds
            public int predictedLapTimeMs;                       // Predicted final lap time in milliseconds
            public float fuelLiterCurrentQuantity;               // Fuel remaining in the tank in litres
            public float fuelLiterCurrentQuantityPercent;        // Fuel remaining as a fraction of tank capacity
            public float fuelLiterPerKm;                         // Average fuel consumption rate in litres per km
            public float kmPerFuelLiter;                         // Average fuel economy in km per litre
            public float currentTorque;                          // Engine output torque in Nm
            public int currentBhp;                               // Engine output power in brake horsepower
            public SMEvoTyreState tyreLf;                        // Full tyre state for the front-left corner
            public SMEvoTyreState tyreRf;                        // Full tyre state for the front-right corner
            public SMEvoTyreState tyreLr;                        // Full tyre state for the rear-left corner
            public SMEvoTyreState tyreRr;                        // Full tyre state for the rear-right corner
            public float npos;                                   // Normalised track position (0.0 = start/finish line, 1.0 = one full lap)
            public float kersChargePerc;                         // KERS/ERS charge level as a fraction (0.0–1.0)
            public float kersCurrentPerc;                        // KERS/ERS power currently being deployed as a fraction
            public float controlLockTime;                        // Seconds driver input remains locked (e.g. after collision penalty)
            public SMEvoDamageState carDamage;                   // Damage levels for each body zone of the car
            public ACEVO_CAR_LOCATION carLocation;               // Current track zone the car occupies (see ACEVO_CAR_LOCATION)
            public SMEvoPitInfo pitInfo;                         // Status of each pit-stop service item
            public float fuelLiterUsed;                          // Fuel consumed since session start in litres
            public float fuelLiterPerLap;                        // Average fuel consumed per lap in litres
            public float lapsPossibleWithFuel;                   // Estimated number of laps achievable with remaining fuel
            public float batteryTemperature;                     // High-voltage battery temperature in °C
            public float batteryVoltage;                         // High-voltage battery pack voltage in V
            public float instantaneousFuelLiterPerKm;            // Instantaneous fuel consumption in litres per km
            public float instantaneousKmPerFuelLiter;            // Instantaneous fuel economy in km per litre
            public float gearRpmWindow;                          // How well current RPM suits the engaged gear (1.0 = ideal window)
            public SMEvoInstrumentation instrumentation;         // Current state of all cockpit lights and displays
            public SMEvoInstrumentation instrumentationMinLimit; // Minimum allowed setting for each instrumentation item
            public SMEvoInstrumentation instrumentationMaxLimit; // Maximum allowed setting for each instrumentation item
            public SMEvoElectronics electronics;                 // Current electronic aid and setup values
            public SMEvoElectronics electronicsMinLimit;         // Minimum allowed value for each electronics setting
            public SMEvoElectronics electronicsMaxLimit;         // Maximum allowed value for each electronics setting
            public SMEvoElectronics electronicsIsModifiable;     // Flags which electronics fields the driver can adjust in-session
            public int totalLapCount;                            // Total laps completed in the session
            public uint currentPos;                              // Current race position (1 = leader)
            public uint totalDrivers;                            // Total number of cars in the session
            public int lastLapTimeMs;                            // Last completed lap time in milliseconds
            public int bestLapTimeMs;                            // Personal best lap time in milliseconds
            public ACEVO_FLAG_TYPE flag;                         // Flag shown specifically to this driver
            public ACEVO_FLAG_TYPE globalFlag;                   // Flag shown to all drivers on track
            public uint maxGears;                                // Number of forward gears the car has
            public ACEVO_ENGINE_TYPE engineType;                 // Powertrain type of the car (see ACEVO_ENGINE_TYPE)
            [MarshalAs(UnmanagedType.I1)]
            public bool hasKers;                                 // Car is equipped with a KERS/ERS system
            [MarshalAs(UnmanagedType.I1)]
            public bool isLastLap;                               // This is the final scheduled lap of the race
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string performanceModeName;                   // Display name of the active vehicle performance / power mode
            public float diffCoastRawValue;                      // Raw differential coast-lock value from setup
            public float diffPowerRawValue;                      // Raw differential power-lock value from setup
            public int raceCutGainedTimeMs;                      // Cumulative time penalty from track-limit cuts in ms
            public int distanceToDeadline;                       // Distance to the penalty trigger in metres
            public float raceCutCurrentDelta;                    // Running delta time accrued from track-limit violations
            public SMEvoSessionState sessionState;               // Session lifecycle and countdown information
            public SMEvoTimingState timingState;                 // HUD lap times and delta display values
            public int playerPing;                               // Network round-trip ping to the server in ms
            public int playerLatency;                            // Measured network latency in ms
            public int playerCpuUsage;                           // Client CPU usage in percent
            public int playerCpuUsageAvg;                        // Average client CPU usage in percent
            public int playerQos;                                // Network Quality-of-Service score
            public int playerQosAvg;                             // Average QoS score over the session
            public int playerFps;                                // Current rendered frames per second
            public int playerFpsAvg;                             // Average FPS over the session
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string driverName;                            // Driver's first name
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string driverSurname;                         // Driver's surname
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string carModel;                              // Identifier or display name of the car model
            [MarshalAs(UnmanagedType.I1)]
            public bool isInPitBox;                              // Car is stationary inside its assigned pit box
            [MarshalAs(UnmanagedType.I1)]
            public bool isInPitLane;                             // Car is anywhere within the pit lane
            [MarshalAs(UnmanagedType.I1)]
            public bool isValidLap;                              // Current lap is valid and counts for timing
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = aceConstant.MaxOpponents)]
            public aceVec3[] carCoordinates;                     // World-space position of up to 60 cars [carIndex][X, Y, Z]
            public float gapAhead;                               // Time gap to the car immediately ahead in seconds
            public float gapBehind;                              // Time gap to the car immediately behind in seconds
            public byte activeCars;                              // Number of cars actively participating in the session
            public float fuelPerLap;                             // Target fuel consumption per lap in litres
            public float fuelEstimatedLaps;                      // Estimated laps remaining with current fuel
            public SMEvoAssistsState assistsState;               // All driver-assist levels currently active
            public float maxFuel;                                // Maximum fuel tank capacity of the car in litres
            public float maxTurboBoost;                          // Maximum turbo boost pressure in bar
            [MarshalAs(UnmanagedType.I1)]
            public bool useSingleCompound;                       // Car is restricted to a single tyre compound for both axles
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 60)]
            public carId[] carIds;                               // Unique ID of the cars in the session
        }

        #endregion

        #region Static

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        public struct SPageFileStaticEvo
        {
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string smVersion;                             // Shared-memory interface version string
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 15)]
            public string acEvoVersion;                          // AC Evo game build version string
            public ACEVO_SESSION_TYPE session;                   // Type of the current session (see ACEVO_SESSION_TYPE)
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string sessionName;                           // Human-readable session name (e.g. 'Race 1')
            public byte eventId;                                 // Unique identifier of the event within the championship
            public byte sessionId;                               // Unique identifier of this session within the event
            public ACEVO_STARTING_GRIP startingGrip;             // Tyre grip condition at session start (see ACEVO_STARTING_GRIP)
            public float startingAmbientTemperatureC;            // Ambient air temperature at session start in °C
            public float startingGroundTemperatureC;             // Road surface temperature at session start in °C
            [MarshalAs(UnmanagedType.I1)]
            public bool isStaticWeather;                         // Weather is fixed and will not change during the session
            [MarshalAs(UnmanagedType.I1)]
            public bool isTimedRace;                             // Session ends by elapsed time rather than lap count
            [MarshalAs(UnmanagedType.I1)]
            public bool isOnline;                                // Session is an online multiplayer event
            public int numberOfSessions;                         // Total sessions in this event (e.g. 3 = practice + qualify + race)
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string nation;                                // Country / nation name associated with the event or track
            public float longitude;                              // Geographic longitude of the track location in decimal degrees
            public float latitude;                               // Geographic latitude of the track location in decimal degrees
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string track;                                 // Track identifier or name
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string trackConfiguration;                    // Track layout variant or configuration name
            public float trackLengthM;                           // Total lap length of the track in metres
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        public struct aceVec3
        {
            public float x;
            public float y;
            public float z;
            public aceVec3(float x, float y, float z)
            {
                this.x = x; this.y = y; this.z = z;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        public struct aceVehicleInfo
        {
            public carId carId;
            public int isPlayerVehicle;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] driverName;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] carModel;
            public float speedMS;
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
            public aceVec3 worldPosition;
            public int isCarInPitlane;                           // car is inside the pit limiter region
            public int isCarInPitEntry;                          // car is within the pit entry region
            public int isCarInPitExit;                           // car is within the pit exit region
            public int carLeaderboardPosition;
            public int carRealTimeLeaderboardPosition;           // for race sessions this is derived from the total distance travelled, for other sessions it's the carLeaderboardPosition
            public float spLineLength;                           // TBD EVO percentage of the way around the track for the start/finish line (0.0–1.0)
            public int isConnected;                              // NOT USED, IS ALWAYS 1
            public int raceNumber;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] suspensionDamage;
            public float engineLifeLeft;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] tyreInflation;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
        public struct SPageFileCrewChief
        {
            //public int numVehicles;
            //public int focusVehicle;
            //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 512)]
            //public byte[] serverName;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
            public aceVehicleInfo[] vehicle;
            //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 512)]
            //public byte[] acInstallPath;
            //public int isInternalMemoryModuleLoaded;
            //[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
            //public byte[] pluginVersion;
            public int playerIndex; // index in the carCoordinates array that corresponds to the player's car.
        }

        public class ACEShared
        {
            public SPageFilePhysics acePhysics;
            public SPageFileGraphicEvo aceGraphic;
            public SPageFileStaticEvo aceStatic;
            public SPageFileCrewChief aceChief;
            /// <summary>
            /// TBD EVO Provide dummy data CC requires
            /// </summary>
            public ACEShared()
            {
                aceChief = new SPageFileCrewChief();
                aceChief.vehicle = new aceVehicleInfo[aceConstant.MaxVehicles];
                aceChief.vehicle[0].tyreInflation = new float[4];
                aceChief.vehicle[0].suspensionDamage = new float[4];
            }
        }
        #endregion

    }
}
