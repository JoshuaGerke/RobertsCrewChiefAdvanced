using Newtonsoft.Json;

using System.Collections.Generic;

namespace CrewChiefV4.LMU
{
    // Classes generated from http://localhost:6397/swagger/index.html responses
    // then VS menu Edit > Paste Special > Paste JSON as Classes
    // or use https://json2csharp.com/ NO. It results in lots of ints that should be floats.
    // Kept in a separate file as they are just noise.
    public class LMU_REST_API_classes
    {
        // Shared Root class for GET & POST pit menus, also used by LMUPitMenuAPI 
        public class PitMenuRoot
        {
            [JsonProperty("PMC Value")]
            public float PMCValue { get; set; }
            public float currentSetting { get; set; }
            public float @default { get; set; }
            public string name { get; set; }
            public List<Setting> settings { get; set; }
        }

        // Shared Setting class
        public class Setting
        {
            public string text { get; set; }
            public bool? isUsed { get; set; }
            public string type { get; set; }
        }

        public class ReceivePitMenu
        {
            internal const string endPoint = "rest/garage/PitMenu/receivePitMenu";
            public class Root : PitMenuRoot { }
        }

        public class LoadPitMenu
        {
            internal const string endPoint = "rest/garage/PitMenu/loadPitMenu";
            public class Root : PitMenuRoot { }
        }

        public class SessionsWeather
        {   // Available in rF2 and LMU. Read as a JObject, it's
            // { "PRACTICE": { "START": { "WNV_SKY": { "currentValue": 0, ...
            internal const string endPoint = "rest/sessions/weather";
            internal static readonly string[] nodeNames = { "START", "NODE_25", "NODE_50", "NODE_75", "FINISH" };
        }

        public class RepairAndRefuel
        { // RC Teaminfo seems to differ from released version
            internal const string endpoint = "rest/garage/UIScreen/RepairAndRefuel";


            public class Root
            {
                public Currentweather currentWeather { get; set; }
                public Fuelinfo fuelInfo { get; set; }
                public Pitmenu pitMenu { get; set; }
                public Pitrecommendations pitRecommendations { get; set; }
                public Pitstoplength pitStopLength { get; set; }
                public Pitstoptimes pitStopTimes { get; set; }
                public Raceposition racePosition { get; set; }
                public Sessiontime sessionTime { get; set; }
                public Teaminfo teamInfo { get; set; }
                public Wearables wearables { get; set; }
                public Weatherforecast weatherForecast { get; set; }
            }

            public class Currentweather
            {
                public float airPressure { get; set; }
                public float ambientTempKelvin { get; set; }
                public float cloudCoverage { get; set; }
                public float humidity { get; set; }
                public float lightLevel { get; set; }
                public float rainIntensity { get; set; }
                public float raining { get; set; }
                public float trackTempKelvin { get; set; }
            }

            public class Fuelinfo
            {
                public float currentBattery { get; set; }
                public float currentFuel { get; set; }
                public float currentVirtualEnergy { get; set; }
                public float maxBattery { get; set; }
                public float maxFuel { get; set; }
                public float maxVirtualEnergy { get; set; }
            }

            public class Pitmenu
            {
                public Pitmenu1[] pitMenu { get; set; }
            }

            public class Pitmenu1
            {
                public float PMCValue { get; set; }
                public float currentSetting { get; set; }
                public float _default { get; set; }
                public string name { get; set; }
                public Setting[] settings { get; set; }
            }

            public class Setting
            {
                public string text { get; set; }
                public bool isUsed { get; set; }
                public string type { get; set; }
            }

            public class Pitrecommendations
            {
                public float FLTIRE { get; set; }
                public float FRTIRE { get; set; }
                public float RLTIRE { get; set; }
                public float RRTIRE { get; set; }
                public float TIRES { get; set; }
                public float fuel { get; set; }
                public float virtualEnergy { get; set; }
            }

            public class Pitstoplength
            {
                public float timeInSeconds { get; set; }
            }

            public class Pitstoptimes
            {
                public Times times { get; set; }
            }

            public class Times
            {
                public float BrakeChange { get; set; }
                public float BrakeTimeConcurrent { get; set; }
                public float DriverChange { get; set; }
                public float DriverConcurrent { get; set; }
                public float DriverDamage { get; set; }
                public float DriverRandom { get; set; }
                public float FenderFlareAdjust { get; set; }
                public float FixAeroDamage { get; set; }
                public float FixAllDamage { get; set; }
                public float FixRandomDelay { get; set; }
                public float FixTimeConcurrent { get; set; }
                public float FourTireChange { get; set; }
                public float FrontWingAdjust { get; set; }
                public float FrontWingReplace { get; set; }
                public float FuelFillRate { get; set; }
                public float FuelInsert { get; set; }
                public float FuelRandomDelay { get; set; }
                public float FuelRemove { get; set; }
                public float FuelTimeConcurrent { get; set; }
                public bool OnTheFlyPressure { get; set; }
                public float PressureChange { get; set; }
                public float RadiatorChange { get; set; }
                public float RandomBrakeDelay { get; set; }
                public float RandomTireDelay { get; set; }
                public float RearWingAdjust { get; set; }
                public float RearWingReplace { get; set; }
                public bool SimultaneousStopGo { get; set; }
                public float SpringRubberChange { get; set; }
                public float TireTimeConcurrent { get; set; }
                public float TrackBarChange { get; set; }
                public float TwoTireChange { get; set; }
                public float WedgeChange { get; set; }
                public float virtualEnergyFillRate { get; set; }
                public float virtualEnergyInsert { get; set; }
                public float virtualEnergyRandomDelay { get; set; }
                public float virtualEnergyRemove { get; set; }
                public float virtualEnergyTimeConcurrent { get; set; }
            }

            public class Raceposition
            {
                public float gapToFirstInClassLaps { get; set; }
                public float gapToFirstInClassTime { get; set; }
                public float gapToLastInClassLaps { get; set; }
                public float gapToLastInClassTime { get; set; }
                public float placeInClass { get; set; }
                public float placeOverall { get; set; }
            }

            public class Sessiontime
            {
                public float timeOfDay { get; set; }
            }

            public class Teaminfo
            {
                public float[][] driverNames { get; set; }
                public string teamName { get; set; }
                public string vehicleName { get; set; }
            }

            public class Wearables
            {
                public Body body { get; set; }
                public float[] brakes { get; set; }
                public float[] suspension { get; set; }
                public float[] tires { get; set; }
            }

            public class Body
            {
                public float aero { get; set; }
                public bool[] detachableParts { get; set; }
            }

            public class Weatherforecast
            {
                public Nodes nodes { get; set; }
            }

            public class Nodes
            {
                public float[] Duration { get; set; }
                public float[] Humidity { get; set; }
                public float[] RainChance { get; set; }
                public float[] Sky { get; set; }
                public float[] StartTime { get; set; }
                public float[] Temperature { get; set; }
                public float[] WindDirection { get; set; }
                public float[] WindSpeed { get; set; }
            }
        }

        public class Sessions
        {
            internal const string endPoint = "rest/sessions/?";

            // Now there is all this crap just to get SESSSET_Fuel_Usage
            public class Root
            {
                public SESSSET_AI_Aggression SESSSET_AI_Aggression { get; set; }
                public SESSSET_AI_Strength SESSSET_AI_Strength { get; set; }
                public SESSSET_Damage_Multi SESSSET_Damage_Multi { get; set; }

                public SESSSET_Finish_Criteria SESSSET_Finish_Criteria
                {
                    get;
                    set;
                }

                public SESSSET_Fuel_Usage SESSSET_Fuel_Usage { get; set; }
                public SESSSET_Grid_Position SESSSET_Grid_Position { get; set; }
                public SESSSET_Mech_Failures SESSSET_Mech_Failures { get; set; }
                public SESSSET_Num_Opponents SESSSET_Num_Opponents { get; set; }

                public SESSSET_Practice_Length SESSSET_Practice_Length
                {
                    get;
                    set;
                }

                public SESSSET_Qualify_Length SESSSET_Qualify_Length
                {
                    get;
                    set;
                }

                public SESSSET_Race_Laps SESSSET_Race_Laps { get; set; }
                public SESSSET_Tire_Wear SESSSET_Tire_Wear { get; set; }
                public SESSSET_Warmup_Length SESSSET_WarmUp_Length { get; set; }
                public SESSSET_Adjust_Frozen SESSSET_adjust_frozen { get; set; }
                public SESSSET_Blue_Flags SESSSET_blue_flags { get; set; }
                public SESSSET_Cut_Rules SESSSET_cut_rules { get; set; }
                public SESSSET_Cuts_Allowed SESSSET_cuts_allowed { get; set; }
                public SESSSET_Flag_Rules SESSSET_flag_rules { get; set; }

                public SESSSET_Force_Formation SESSSET_force_formation
                {
                    get;
                    set;
                }

                public SESSSET_Formation SESSSET_formation { get; set; }

                public SESSSET_Keep_Tire_Inv_On_Track_Change
                    SESSSET_keep_tire_inv_on_track_change { get; set; }

                public SESSSET_Limited_Tire_Rules SESSSET_limited_tire_rules
                {
                    get;
                    set;
                }

                public SESSSET_Num_Qual_Sessions SESSSET_num_qual_sessions
                {
                    get;
                    set;
                }

                public SESSSET_Num_Race_Sessions SESSSET_num_race_sessions
                {
                    get;
                    set;
                }

                public SESSSET_Parc_Ferme SESSSET_parc_ferme { get; set; }
                public SESSSET_Pract1 SESSSET_pract1 { get; set; }

                public SESSSET_Pract1_Realroad_Init SESSSET_pract1_realroad_init
                {
                    get;
                    set;
                }

                public SESSSET_Pract1_Realroad_Temperatures
                    SESSSET_pract1_realroad_temperatures { get; set; }

                public SESSSET_Pract1_Realroad_Wet SESSSET_pract1_realroad_wet
                {
                    get;
                    set;
                }

                public SESSSET_Pract2 SESSSET_pract2 { get; set; }
                public SESSSET_Pract3 SESSSET_pract3 { get; set; }
                public SESSSET_Pract4 SESSSET_pract4 { get; set; }

                public SESSSET_Practice1_Starting_Time
                    SESSSET_practice1_starting_time { get; set; }

                public SESSSET_Private_Prac SESSSET_private_prac { get; set; }
                public SESSSET_Private_Qual SESSSET_private_qual { get; set; }

                public SESSSET_Qual1_Realroad_Init SESSSET_qual1_realroad_init
                {
                    get;
                    set;
                }

                public SESSSET_Qual1_Realroad_Temperatures
                    SESSSET_qual1_realroad_temperatures { get; set; }

                public SESSSET_Qual1_Realroad_Wet SESSSET_qual1_realroad_wet
                {
                    get;
                    set;
                }

                public SESSSET_Qualify_Starting_Time
                    SESSSET_qualify_starting_time { get; set; }

                public SESSSET_Race_Realroad_Init SESSSET_race_realroad_init
                {
                    get;
                    set;
                }

                public SESSSET_Race_Realroad_Temperatures
                    SESSSET_race_realroad_temperatures { get; set; }

                public SESSSET_Race_Realroad_Wet SESSSET_race_realroad_wet
                {
                    get;
                    set;
                }

                public SESSSET_Race_Starting_Time SESSSET_race_starting_time
                {
                    get;
                    set;
                }

                public SESSSET_Race_Time SESSSET_race_time { get; set; }
                public SESSSET_Race_Timer SESSSET_race_timer { get; set; }

                public SESSSET_Race_Timescale SESSSET_race_timescale
                {
                    get;
                    set;
                }

                public SESSSET_Realroad_Timescale_Practice
                    SESSSET_realroad_timescale_practice { get; set; }

                public SESSSET_Realroad_Timescale_Qualify
                    SESSSET_realroad_timescale_qualify { get; set; }

                public SESSSET_Realroad_Timescale_Race
                    SESSSET_realroad_timescale_race { get; set; }

                public SESSSET_Recon_Pit_Closed SESSSET_recon_pit_closed
                {
                    get;
                    set;
                }

                public SESSSET_Recon_Pit_Open SESSSET_recon_pit_open
                {
                    get;
                    set;
                }

                public SESSSET_Recon_Timer SESSSET_recon_timer { get; set; }

                public SESSSET_Reconnaissance SESSSET_reconnaissance
                {
                    get;
                    set;
                }

                public SESSSET_Run_Warmup SESSSET_run_warmup { get; set; }

                public SESSSET_Safetycar_Thresh SESSSET_safetycar_thresh
                {
                    get;
                    set;
                }

                public SESSSET_Safetycarcollision SESSSET_safetycarcollision
                {
                    get;
                    set;
                }

                public SESSSET_Timescaled_Weather SESSSET_timescaled_weather
                {
                    get;
                    set;
                }

                public SESSSET_Tire_Warmers SESSSET_tire_warmers { get; set; }

                public SESSSET_Tires_Available_In_Garage
                    SESSSET_tires_available_in_garage { get; set; }

                public SESSSET_Unsportsmanlike SESSSET_unsportsmanlike
                {
                    get;
                    set;
                }

                public SESSSET_Walkthrough SESSSET_walkthrough { get; set; }

                public SESSSET_Warmup_Starting_Time SESSSET_warmup_starting_time
                {
                    get;
                    set;
                }

                public SESSSET_Weather SESSSET_weather { get; set; }
            }

            public class SESSSET_AI_Aggression
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_AI_Strength
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Damage_Multi
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Finish_Criteria
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Fuel_Usage
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Grid_Position
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Mech_Failures
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Num_Opponents
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Practice_Length
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Qualify_Length
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Laps
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Tire_Wear
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Warmup_Length
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Adjust_Frozen
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Blue_Flags
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Cut_Rules
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Cuts_Allowed
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Flag_Rules
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Force_Formation
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Formation
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Keep_Tire_Inv_On_Track_Change
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Limited_Tire_Rules
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Num_Qual_Sessions
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Num_Race_Sessions
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Parc_Ferme
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract1
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract1_Realroad_Init
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract1_Realroad_Temperatures
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract1_Realroad_Wet
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract2
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract3
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Pract4
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Practice1_Starting_Time
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Private_Prac
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Private_Qual
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Qual1_Realroad_Init
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Qual1_Realroad_Temperatures
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Qual1_Realroad_Wet
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Qualify_Starting_Time
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Realroad_Init
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Realroad_Temperatures
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Realroad_Wet
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Starting_Time
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Time
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Timer
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Race_Timescale
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Realroad_Timescale_Practice
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Realroad_Timescale_Qualify
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Realroad_Timescale_Race
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Recon_Pit_Closed
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Recon_Pit_Open
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Recon_Timer
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Reconnaissance
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Run_Warmup
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Safetycar_Thresh
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Safetycarcollision
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Timescaled_Weather
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Tire_Warmers
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Tires_Available_In_Garage
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Unsportsmanlike
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Walkthrough
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Warmup_Starting_Time
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }

            public class SESSSET_Weather
            {
                public float currentValue { get; set; }
                public float numStepsTotal { get; set; }
                public float settingID { get; set; }
                public string stringValue { get; set; }
                public string uiSelectionType { get; set; }
                public string valueType { get; set; }
            }
        }


        public class OptionsSettings
        {
            // Now there is all this crap just to get DRIVEAIDS_invulnerable
            internal const string endPoint = "rest/options/settings";

            public class Root
            {
                public CONTROL_Auto_Reverse CONTROL_auto_reverse { get; set; }
                public object COPY_practice1_time { get; set; }
                public object COPY_qualify_laps { get; set; }
                public object COPY_qualify_time { get; set; }
                public object COPY_warmup_time { get; set; }
                public DISPLAYOPT_Showroom DISPLAYOPT_showroom { get; set; }

                public DRIVEAIDS_Antilock_Brakes DRIVEAIDS_antilock_brakes
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Auto_Blip DRIVEAIDS_auto_blip { get; set; }
                public DRIVEAIDS_Auto_Clutch DRIVEAIDS_auto_clutch { get; set; }

                public DRIVEAIDS_Auto_Headlights DRIVEAIDS_auto_headlights
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Auto_Lift DRIVEAIDS_auto_lift { get; set; }
                public DRIVEAIDS_Auto_Wipers DRIVEAIDS_auto_wipers { get; set; }

                public DRIVEAIDS_Automatic_Pit_Speed_Limit
                    DRIVEAIDS_automatic_pit_speed_limit { get; set; }

                public DRIVEAIDS_Autopit DRIVEAIDS_autopit { get; set; }
                public DRIVEAIDS_Brake_Help DRIVEAIDS_brake_help { get; set; }
                public DRIVEAIDS_Hold_Brakes DRIVEAIDS_hold_brakes { get; set; }
                public DRIVEAIDS_Hold_Clutch DRIVEAIDS_hold_clutch { get; set; }

                public DRIVEAIDS_Invulnerable DRIVEAIDS_invulnerable
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Opposite_Lock DRIVEAIDS_opposite_lock
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Repeat_Shifts DRIVEAIDS_repeat_shifts
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Shift_Mode DRIVEAIDS_shift_mode { get; set; }

                public DRIVEAIDS_Spin_Recovery DRIVEAIDS_spin_recovery
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Stability_Control DRIVEAIDS_stability_control
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Start_Engine DRIVEAIDS_start_engine
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Steering_Help DRIVEAIDS_steering_help
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Throttle_Control DRIVEAIDS_throttle_control
                {
                    get;
                    set;
                }

                public DRIVEAIDS_Vis_Fast_Line DRIVEAIDS_vis_fast_line
                {
                    get;
                    set;
                }

                public object GAMEOPT_MULTI_race_finish_criteria { get; set; }
                public object GAMEOPT_MULTI_race_laps { get; set; }
                public object GAMEOPT_MULTI_race_time { get; set; }
                public GAMEOPT_Ai_Aggression GAMEOPT_ai_aggression { get; set; }

                public GAMEOPT_Ai_Driverstrength GAMEOPT_ai_driverstrength
                {
                    get;
                    set;
                }

                public GAMEOPT_Broadcast_Custom_Cameras
                    GAMEOPT_broadcast_custom_cameras { get; set; }

                public GAMEOPT_Broadcast_Group_Lap1range
                    GAMEOPT_broadcast_group_lap1range { get; set; }

                public GAMEOPT_Broadcast_Group_Mode GAMEOPT_broadcast_group_mode
                {
                    get;
                    set;
                }

                public GAMEOPT_Broadcast_Group_Range
                    GAMEOPT_broadcast_group_range { get; set; }

                public GAMEOPT_Broadcast_Group_Zoommax
                    GAMEOPT_broadcast_group_zoommax { get; set; }

                public GAMEOPT_Broadcast_Group_Zoomstrength
                    GAMEOPT_broadcast_group_zoomstrength { get; set; }

                public GAMEOPT_Broadcast_Persistent_Zoom
                    GAMEOPT_broadcast_persistent_zoom { get; set; }

                public GAMEOPT_Broadcast_Trackingchange_Blend
                    GAMEOPT_broadcast_trackingchange_blend { get; set; }

                public GAMEOPT_Broadcast_Trackingchange_Maxtimediff
                    GAMEOPT_broadcast_trackingchange_maxtimediff { get; set; }

                public GAMEOPT_Broadcast_Trackingchange_Timedelay
                    GAMEOPT_broadcast_trackingchange_timedelay { get; set; }

                public GAMEOPT_Damagemultiplier GAMEOPT_damagemultiplier
                {
                    get;
                    set;
                }

                public GAMEOPT_Delete_Save_Data_Race_End
                    GAMEOPT_delete_save_data_race_end { get; set; }

                public GAMEOPT_Kph GAMEOPT_kph { get; set; }
                public GAMEOPT_Kw GAMEOPT_kw { get; set; }

                public GAMEOPT_Remember_Onboard_Changes
                    GAMEOPT_remember_onboard_changes { get; set; }

                public GAMEOPT_Spotter_Detail GAMEOPT_spotter_detail
                {
                    get;
                    set;
                }

                public GAMEOPT_Spotter_Laptimes GAMEOPT_spotter_laptimes
                {
                    get;
                    set;
                }

                public GAMEOPT_Transparent_Trainer_Lap
                    GAMEOPT_transparent_trainer_lap { get; set; }

                public GAMEOPT_Transparent_Trainer_Lead_Time
                    GAMEOPT_transparent_trainer_lead_time { get; set; }

                public GAMEOPT_Units GAMEOPT_units { get; set; }

                public GAMEOPT_Wait_For_Plugins GAMEOPT_wait_for_plugins
                {
                    get;
                    set;
                }

                public GRAPHOPT_HUD_Ultrawide_Ratio GRAPHOPT_HUD_ultrawide_ratio
                {
                    get;
                    set;
                }

                public GRAPHOPT_Texturefilter GRAPHOPT_TextureFilter
                {
                    get;
                    set;
                }

                public GRAPHOPT_VR_IPD_Scale GRAPHOPT_VR_IPD_scale { get; set; }
                public GRAPHOPT_VR_Hud_Depth GRAPHOPT_VR_hud_depth { get; set; }
                public GRAPHOPT_VR_Hud_Scale GRAPHOPT_VR_hud_scale { get; set; }

                public GRAPHOPT_VR_Menu_Depth GRAPHOPT_VR_menu_depth
                {
                    get;
                    set;
                }

                public GRAPHOPT_VR_Menu_Scale GRAPHOPT_VR_menu_scale
                {
                    get;
                    set;
                }

                public GRAPHOPT_Car_Vibration_Mult1 GRAPHOPT_car_vibration_mult1
                {
                    get;
                    set;
                }

                public GRAPHOPT_Driver_Labels GRAPHOPT_driver_labels
                {
                    get;
                    set;
                }

                public GRAPHOPT_Driver_Overlay_Flags
                    GRAPHOPT_driver_overlay_flags { get; set; }

                public GRAPHOPT_Driver_Overlay_Hud GRAPHOPT_driver_overlay_hud
                {
                    get;
                    set;
                }

                public GRAPHOPT_Driver_Overlay_Info_Car
                    GRAPHOPT_driver_overlay_info_car { get; set; }

                public GRAPHOPT_Driver_Overlay_Info_Timing
                    GRAPHOPT_driver_overlay_info_timing { get; set; }

                public GRAPHOPT_Driver_Overlay_Notifications_Chat
                    GRAPHOPT_driver_overlay_notifications_chat { get; set; }

                public GRAPHOPT_Driver_Overlay_Notifications_Game
                    GRAPHOPT_driver_overlay_notifications_game { get; set; }

                public GRAPHOPT_Driver_Overlay_Notifications_Race
                    GRAPHOPT_driver_overlay_notifications_race { get; set; }

                public GRAPHOPT_Driver_Overlay_Radar
                    GRAPHOPT_driver_overlay_radar { get; set; }

                public GRAPHOPT_Driver_Overlay_Standings
                    GRAPHOPT_driver_overlay_standings { get; set; }

                public GRAPHOPT_Driver_Overlay_Trackmap
                    GRAPHOPT_driver_overlay_trackmap { get; set; }

                public GRAPHOPT_Env_Reflections GRAPHOPT_env_reflections
                {
                    get;
                    set;
                }

                public GRAPHOPT_Exaggerate_Yaw GRAPHOPT_exaggerate_yaw
                {
                    get;
                    set;
                }

                public GRAPHOPT_Head_Physics GRAPHOPT_head_physics { get; set; }

                public GRAPHOPT_Live_Tv_Displays GRAPHOPT_live_tv_displays
                {
                    get;
                    set;
                }

                public GRAPHOPT_Lookahead_Radians GRAPHOPT_lookahead_radians
                {
                    get;
                    set;
                }

                public GRAPHOPT_Max_Framerate GRAPHOPT_max_framerate
                {
                    get;
                    set;
                }

                public GRAPHOPT_Motionblur GRAPHOPT_motionblur { get; set; }

                public GRAPHOPT_Multiview_Center_Bezelgap
                    GRAPHOPT_multiview_center_BezelGap { get; set; }

                public GRAPHOPT_Multiview_Center_Eyedistance
                    GRAPHOPT_multiview_center_EyeDistance { get; set; }

                public GRAPHOPT_Multiview_Center_Screenheight
                    GRAPHOPT_multiview_center_ScreenHeight { get; set; }

                public GRAPHOPT_Multiview_Center_Screenwidth
                    GRAPHOPT_multiview_center_ScreenWidth { get; set; }

                public GRAPHOPT_Multiview_Center_Sideangle
                    GRAPHOPT_multiview_center_SideAngle { get; set; }

                public GRAPHOPT_Multiview_Left_Bezelgap
                    GRAPHOPT_multiview_left_BezelGap { get; set; }

                public GRAPHOPT_Multiview_Left_Eyedistance
                    GRAPHOPT_multiview_left_EyeDistance { get; set; }

                public GRAPHOPT_Multiview_Left_Screenheight
                    GRAPHOPT_multiview_left_ScreenHeight { get; set; }

                public GRAPHOPT_Multiview_Left_Screenwidth
                    GRAPHOPT_multiview_left_ScreenWidth { get; set; }

                public GRAPHOPT_Multiview_Left_Sideangle
                    GRAPHOPT_multiview_left_SideAngle { get; set; }

                public GRAPHOPT_Multiview_Mode GRAPHOPT_multiview_mode
                {
                    get;
                    set;
                }

                public GRAPHOPT_Multiview_Right_Bezelgap
                    GRAPHOPT_multiview_right_BezelGap { get; set; }

                public GRAPHOPT_Multiview_Right_Eyedistance
                    GRAPHOPT_multiview_right_EyeDistance { get; set; }

                public GRAPHOPT_Multiview_Right_Screenheight
                    GRAPHOPT_multiview_right_ScreenHeight { get; set; }

                public GRAPHOPT_Multiview_Right_Screenwidth
                    GRAPHOPT_multiview_right_ScreenWidth { get; set; }

                public GRAPHOPT_Multiview_Right_Sideangle
                    GRAPHOPT_multiview_right_SideAngle { get; set; }

                public GRAPHOPT_Multiview_Shared_Bezelgap
                    GRAPHOPT_multiview_shared_BezelGap { get; set; }

                public GRAPHOPT_Multiview_Shared_Eyedistance
                    GRAPHOPT_multiview_shared_EyeDistance { get; set; }

                public GRAPHOPT_Multiview_Shared_Screenheight
                    GRAPHOPT_multiview_shared_ScreenHeight { get; set; }

                public GRAPHOPT_Multiview_Shared_Screenwidth
                    GRAPHOPT_multiview_shared_ScreenWidth { get; set; }

                public GRAPHOPT_Multiview_Shared_Sideangle
                    GRAPHOPT_multiview_shared_SideAngle { get; set; }

                public GRAPHOPT_Opponent_Detail GRAPHOPT_opponent_detail
                {
                    get;
                    set;
                }

                public GRAPHOPT_Player_Detail GRAPHOPT_player_detail
                {
                    get;
                    set;
                }

                public GRAPHOPT_Rain_Drops GRAPHOPT_rain_drops { get; set; }
                public GRAPHOPT_Rearview GRAPHOPT_rearview { get; set; }

                public GRAPHOPT_Road_Reflections GRAPHOPT_road_reflections
                {
                    get;
                    set;
                }

                public GRAPHOPT_Screenshot_Depth_Alpha
                    GRAPHOPT_screenshot_depth_alpha { get; set; }

                public GRAPHOPT_Screenshot_File_Type
                    GRAPHOPT_screenshot_file_type { get; set; }

                public GRAPHOPT_Shadow_Blur GRAPHOPT_shadow_blur { get; set; }
                public GRAPHOPT_Shadows GRAPHOPT_shadows { get; set; }

                public GRAPHOPT_Soft_Particles GRAPHOPT_soft_particles
                {
                    get;
                    set;
                }

                public GRAPHOPT_Specialfx GRAPHOPT_specialfx { get; set; }
                public GRAPHOPT_Ssao GRAPHOPT_ssao { get; set; }

                public GRAPHOPT_Stabilize_Horizon GRAPHOPT_stabilize_horizon
                {
                    get;
                    set;
                }

                public GRAPHOPT_Starting_View GRAPHOPT_starting_view
                {
                    get;
                    set;
                }

                public GRAPHOPT_Steering_Wheel GRAPHOPT_steering_wheel
                {
                    get;
                    set;
                }

                public GRAPHOPT_Texture GRAPHOPT_texture { get; set; }
                public GRAPHOPT_Track GRAPHOPT_track { get; set; }
                public GRAPHOPT_Vfov GRAPHOPT_vfov { get; set; }

                public GRAPHOPT_Visible_Vehicles GRAPHOPT_visible_vehicles
                {
                    get;
                    set;
                }

                public MP_NETWORK_Connection_Type MP_NETWORK_connection_type
                {
                    get;
                    set;
                }

                public MP_NETWORK_Downstream MP_NETWORK_downstream { get; set; }
                public MP_NETWORK_Upstream MP_NETWORK_upstream { get; set; }

                public MULTI_Download_Custom_Skins MULTI_download_custom_skins
                {
                    get;
                    set;
                }

                public REPLAYOPT_Instant_Replay REPLAYOPT_instant_replay
                {
                    get;
                    set;
                }

                public REPLAYOPT_Record_Hotlaps REPLAYOPT_record_hotlaps
                {
                    get;
                    set;
                }

                public REPLAYOPT_Record_Replays REPLAYOPT_record_replays
                {
                    get;
                    set;
                }

                public REPLAYOPT_Replay_Fidelity REPLAYOPT_replay_fidelity
                {
                    get;
                    set;
                }

                public SERVEROPT_HTTP_Server_Document_Path
                    SERVEROPT_HTTP_server_document_path { get; set; }

                public SERVEROPT_HTTP_Server_Max_File_Size
                    SERVEROPT_HTTP_server_max_file_size { get; set; }

                public SERVEROPT_HTTP_Server_Send_Rate
                    SERVEROPT_HTTP_server_send_rate { get; set; }

                public SERVEROPT_Admin_Func SERVEROPT_admin_func { get; set; }

                public SERVEROPT_Admin_Password SERVEROPT_admin_password
                {
                    get;
                    set;
                }

                public object SERVEROPT_aid_max { get; set; }

                public SERVEROPT_Allow_Ai_Toggling SERVEROPT_allow_ai_toggling
                {
                    get;
                    set;
                }

                public SERVEROPT_Allow_Any_Event SERVEROPT_allow_any_event
                {
                    get;
                    set;
                }

                public SERVEROPT_Allow_Hotlap_Completion
                    SERVEROPT_allow_hotlap_completion { get; set; }

                public SERVEROPT_Allow_Loose_Content_Transfer
                    SERVEROPT_allow_loose_content_transfer { get; set; }

                public SERVEROPT_Allow_Race_Rejoin SERVEROPT_allow_race_rejoin
                {
                    get;
                    set;
                }

                public SERVEROPT_Allows_Driver_Hotswap
                    SERVEROPT_allows_driver_hotswap { get; set; }

                public SERVEROPT_Allows_Spectator_Chat
                    SERVEROPT_allows_spectator_chat { get; set; }

                public SERVEROPT_Assign_Parking SERVEROPT_assign_parking
                {
                    get;
                    set;
                }

                public SERVEROPT_Client_Fuel_Visible
                    SERVEROPT_client_fuel_visible { get; set; }

                public SERVEROPT_Client_Wait SERVEROPT_client_wait { get; set; }

                public SERVEROPT_Closed_Qualify_Session
                    SERVEROPT_closed_qualify_session { get; set; }

                public SERVEROPT_Closed_Race_Session
                    SERVEROPT_closed_race_session { get; set; }

                public SERVEROPT_Coll_Threshold SERVEROPT_coll_threshold
                {
                    get;
                    set;
                }

                public SERVEROPT_Dedicated_Loading_Prio
                    SERVEROPT_dedicated_loading_prio { get; set; }

                public SERVEROPT_Dedicated_Loading_Sleep
                    SERVEROPT_dedicated_loading_sleep { get; set; }

                public SERVEROPT_Default_Name SERVEROPT_default_name
                {
                    get;
                    set;
                }

                public SERVEROPT_Delay_After_Race SERVEROPT_delay_after_race
                {
                    get;
                    set;
                }

                public SERVEROPT_Delay_Between_Sessions
                    SERVEROPT_delay_between_sessions { get; set; }

                public SERVEROPT_Enable_Autodownloads
                    SERVEROPT_enable_autodownloads { get; set; }

                public SERVEROPT_Enforce_Real_Name SERVEROPT_enforce_real_name
                {
                    get;
                    set;
                }

                public SERVEROPT_Force_Driving_View SERVEROPT_force_driving_view
                {
                    get;
                    set;
                }

                public SERVEROPT_Isolation_Code SERVEROPT_isolation_code
                {
                    get;
                    set;
                }

                public SERVEROPT_Join_Password SERVEROPT_join_password
                {
                    get;
                    set;
                }

                public SERVEROPT_Lessen_Restrictions
                    SERVEROPT_lessen_restrictions { get; set; }

                public SERVEROPT_Max_Clients SERVEROPT_max_clients { get; set; }

                public SERVEROPT_Max_Kbps_Per_Client
                    SERVEROPT_max_kbps_per_client { get; set; }

                public SERVEROPT_Max_Vehicles SERVEROPT_max_vehicles
                {
                    get;
                    set;
                }

                public SERVEROPT_Maximum_AI SERVEROPT_maximum_AI { get; set; }
                public SERVEROPT_Minimum_AI SERVEROPT_minimum_AI { get; set; }

                public SERVEROPT_Must_Be_Stopped SERVEROPT_must_be_stopped
                {
                    get;
                    set;
                }

                public SERVEROPT_Pause_If_No_Humans SERVEROPT_pause_if_no_humans
                {
                    get;
                    set;
                }

                public SERVEROPT_Pause_Start_Of_First_Session
                    SERVEROPT_pause_start_of_first_session { get; set; }

                public SERVEROPT_Pit_Speed_Override SERVEROPT_pit_speed_override
                {
                    get;
                    set;
                }

                public SERVEROPT_Plugin_Heartbeat_Rate
                    SERVEROPT_plugin_heartbeat_rate { get; set; }

                public SERVEROPT_Practice1_Time SERVEROPT_practice1_time
                {
                    get;
                    set;
                }

                public SERVEROPT_Qualify_Laps SERVEROPT_qualify_laps
                {
                    get;
                    set;
                }

                public SERVEROPT_Qualify_Time SERVEROPT_qualify_time
                {
                    get;
                    set;
                }

                public SERVEROPT_Session_End_Timeout
                    SERVEROPT_session_end_timeout { get; set; }

                public SERVEROPT_Superadmin_Password
                    SERVEROPT_superadmin_password { get; set; }

                public SERVEROPT_Test_Day SERVEROPT_test_day { get; set; }

                public SERVEROPT_Unique_Vehicles SERVEROPT_unique_vehicles
                {
                    get;
                    set;
                }

                public object SERVEROPT_unthrottle_id { get; set; }

                public SERVEROPT_Unthrottle_Prefix SERVEROPT_unthrottle_prefix
                {
                    get;
                    set;
                }

                public SERVEROPT_Vote_Max_Race_Restarts
                    SERVEROPT_vote_max_race_restarts { get; set; }

                public SERVEROPT_Vote_Min_Voters SERVEROPT_vote_min_voters
                {
                    get;
                    set;
                }

                public SERVEROPT_Vote_Pct_Addai SERVEROPT_vote_pct_addai
                {
                    get;
                    set;
                }

                public SERVEROPT_Vote_Pct_Nextsession
                    SERVEROPT_vote_pct_nextsession { get; set; }

                public SERVEROPT_Vote_Pct_Other SERVEROPT_vote_pct_other
                {
                    get;
                    set;
                }

                public SERVEROPT_Warmup_Time SERVEROPT_warmup_time { get; set; }
                public SOUNDOPT_Channels SOUNDOPT_Channels { get; set; }
                public SOUNDOPT_Engine_Vol SOUNDOPT_engine_vol { get; set; }

                public SOUNDOPT_External_Vol_Ratio SOUNDOPT_external_vol_ratio
                {
                    get;
                    set;
                }

                public SOUNDOPT_Hrtf SOUNDOPT_hrtf { get; set; }
                public SOUNDOPT_Master_Vol SOUNDOPT_master_vol { get; set; }
                public SOUNDOPT_Max_Effects SOUNDOPT_max_effects { get; set; }
                public SOUNDOPT_Mic_Mode SOUNDOPT_mic_mode { get; set; }
                public SOUNDOPT_Onboard_Vol SOUNDOPT_onboard_vol { get; set; }
                public SOUNDOPT_Options_Vol SOUNDOPT_options_vol { get; set; }

                public SOUNDOPT_Player_Vol_Ratio SOUNDOPT_player_vol_ratio
                {
                    get;
                    set;
                }

                public SOUNDOPT_Soundfx_Vol SOUNDOPT_soundfx_vol { get; set; }
                public SOUNDOPT_Spotter_Vol SOUNDOPT_spotter_vol { get; set; }

                public SOUNDOPT_Tire_Vol_Ratio SOUNDOPT_tire_vol_ratio
                {
                    get;
                    set;
                }

                public object VIDEOOPT_APPLY_CHANGES { get; set; }
                public VIDEOOPT_MULTIVIEW VIDEOOPT_MULTIVIEW { get; set; }
                public VIDEOOPT_POSTFXAA VIDEOOPT_POSTFXAA { get; set; }
                public VIDEOOPT_VIDEOMODE VIDEOOPT_VIDEOMODE { get; set; }
                public VIDEOOPT_Vidmaxhres VIDEOOPT_VIDMaxHRes { get; set; }
                public VIDEOOPT_Vidmaxvres VIDEOOPT_VIDMaxVRes { get; set; }
                public VIDEOOPT_Vidminhres VIDEOOPT_VIDMinHRes { get; set; }
                public VIDEOOPT_Vidminvres VIDEOOPT_VIDMinVRes { get; set; }
                public VIDEOOPT_Viddriver VIDEOOPT_VIDdriver { get; set; }
                public VIDEOOPT_Vidfsaa VIDEOOPT_VIDfsaa { get; set; }
                public VIDEOOPT_Vidrefresh VIDEOOPT_VIDrefresh { get; set; }
                public VIDEOOPT_Vidvsync VIDEOOPT_VIDvsync { get; set; }

                public VIDEOOPT_Vidwidescreenhud VIDEOOPT_VIDwidescreenHUD
                {
                    get;
                    set;
                }

                public VIDEOOPT_Vidwidescreenui VIDEOOPT_VIDwidescreenUI
                {
                    get;
                    set;
                }

                public VIDEOOPT_Vidwindowmode VIDEOOPT_VIDwindowMode
                {
                    get;
                    set;
                }

                public VIDEOOPT_VR_Setting VIDEOOPT_VR_setting { get; set; }

                public VIDEOOPT_Postprocess_Level VIDEOOPT_postprocess_level
                {
                    get;
                    set;
                }
            }

            public class CONTROL_Auto_Reverse
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DISPLAYOPT_Showroom
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Antilock_Brakes
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Auto_Blip
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Auto_Clutch
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Auto_Headlights
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Auto_Lift
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Auto_Wipers
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Automatic_Pit_Speed_Limit
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Autopit
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Brake_Help
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Hold_Brakes
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Hold_Clutch
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Invulnerable
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Opposite_Lock
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Repeat_Shifts
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Shift_Mode
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Spin_Recovery
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Stability_Control
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Start_Engine
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Steering_Help
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Throttle_Control
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class DRIVEAIDS_Vis_Fast_Line
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Ai_Aggression
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Ai_Driverstrength
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Custom_Cameras
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Group_Lap1range
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Group_Mode
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Group_Range
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Group_Zoommax
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Group_Zoomstrength
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Persistent_Zoom
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Trackingchange_Blend
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Trackingchange_Maxtimediff
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Broadcast_Trackingchange_Timedelay
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Damagemultiplier
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Delete_Save_Data_Race_End
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Kph
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Kw
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Remember_Onboard_Changes
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Spotter_Detail
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Spotter_Laptimes
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Transparent_Trainer_Lap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Transparent_Trainer_Lead_Time
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Units
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GAMEOPT_Wait_For_Plugins
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_HUD_Ultrawide_Ratio
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Texturefilter
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_VR_IPD_Scale
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_VR_Hud_Depth
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_VR_Hud_Scale
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_VR_Menu_Depth
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_VR_Menu_Scale
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Car_Vibration_Mult1
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Labels
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Flags
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Hud
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Info_Car
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Info_Timing
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Notifications_Chat
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Notifications_Game
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Notifications_Race
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Radar
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Standings
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Driver_Overlay_Trackmap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Env_Reflections
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Exaggerate_Yaw
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Head_Physics
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Live_Tv_Displays
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Lookahead_Radians
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Max_Framerate
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Motionblur
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Center_Bezelgap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Center_Eyedistance
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Center_Screenheight
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Center_Screenwidth
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Center_Sideangle
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Left_Bezelgap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Left_Eyedistance
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Left_Screenheight
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Left_Screenwidth
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Left_Sideangle
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Mode
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Right_Bezelgap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Right_Eyedistance
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Right_Screenheight
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Right_Screenwidth
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Right_Sideangle
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Shared_Bezelgap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Shared_Eyedistance
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Shared_Screenheight
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Shared_Screenwidth
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Multiview_Shared_Sideangle
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Opponent_Detail
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Player_Detail
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Rain_Drops
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Rearview
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Road_Reflections
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Screenshot_Depth_Alpha
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Screenshot_File_Type
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Shadow_Blur
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Shadows
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Soft_Particles
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Specialfx
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Ssao
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Stabilize_Horizon
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Starting_View
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Steering_Wheel
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Texture
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Track
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Vfov
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class GRAPHOPT_Visible_Vehicles
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class MP_NETWORK_Connection_Type
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class MP_NETWORK_Downstream
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class MP_NETWORK_Upstream
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class MULTI_Download_Custom_Skins
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class REPLAYOPT_Instant_Replay
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class REPLAYOPT_Record_Hotlaps
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class REPLAYOPT_Record_Replays
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class REPLAYOPT_Replay_Fidelity
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_HTTP_Server_Document_Path
            {
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_HTTP_Server_Max_File_Size
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_HTTP_Server_Send_Rate
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Admin_Func
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Admin_Password
            {
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allow_Ai_Toggling
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allow_Any_Event
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allow_Hotlap_Completion
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allow_Loose_Content_Transfer
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allow_Race_Rejoin
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allows_Driver_Hotswap
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Allows_Spectator_Chat
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Assign_Parking
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Client_Fuel_Visible
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Client_Wait
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Closed_Qualify_Session
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Closed_Race_Session
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Coll_Threshold
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Dedicated_Loading_Prio
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Dedicated_Loading_Sleep
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Default_Name
            {
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Delay_After_Race
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Delay_Between_Sessions
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Enable_Autodownloads
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Enforce_Real_Name
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Force_Driving_View
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Isolation_Code
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Join_Password
            {
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Lessen_Restrictions
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Max_Clients
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Max_Kbps_Per_Client
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Max_Vehicles
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Maximum_AI
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Minimum_AI
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Must_Be_Stopped
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Pause_If_No_Humans
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Pause_Start_Of_First_Session
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Pit_Speed_Override
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Plugin_Heartbeat_Rate
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Practice1_Time
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Qualify_Laps
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Qualify_Time
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Session_End_Timeout
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Superadmin_Password
            {
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Test_Day
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Unique_Vehicles
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Unthrottle_Prefix
            {
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Vote_Max_Race_Restarts
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Vote_Min_Voters
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Vote_Pct_Addai
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Vote_Pct_Nextsession
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Vote_Pct_Other
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SERVEROPT_Warmup_Time
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Channels
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Engine_Vol
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_External_Vol_Ratio
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Hrtf
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Master_Vol
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Max_Effects
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Mic_Mode
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Onboard_Vol
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Options_Vol
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Player_Vol_Ratio
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Soundfx_Vol
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Spotter_Vol
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class SOUNDOPT_Tire_Vol_Ratio
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_MULTIVIEW
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_POSTFXAA
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_VIDEOMODE
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidmaxhres
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidmaxvres
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidminhres
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidminvres
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Viddriver
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidfsaa
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidrefresh
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidvsync
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidwidescreenhud
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidwidescreenui
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Vidwindowmode
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_VR_Setting
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }

            public class VIDEOOPT_Postprocess_Level
            {
                public float currentValue { get; set; }
                public float maxValue { get; set; }
                public float minValue { get; set; }
                public float stepValue { get; set; }
                public string stringValue { get; set; }
                public string valueType { get; set; }
            }
        }
    }
}
