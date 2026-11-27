using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using iRSDKSharp;

namespace CrewChiefV4.iRacing
{
	[Serializable]
	public class iRacingData
	{
        private static readonly string[] FrameIdentityFields = { "SessionTick", "SessionNum", "PlayerCarIdx" };

        internal iRacingData(byte[] sample, Dictionary<string, CVarHeader> variables, int sessionUpdate, bool is360HzTelemetry)
        {
            object read(string name) => variables.TryGetValue(name, out CVarHeader variable)
                ? iRacingSDK.DecodeTelemetry(sample, variable) : 0;

            foreach (string name in FrameIdentityFields)
            {
                if (!variables.TryGetValue(name, out CVarHeader variable) ||
                    variable.Type != CVarHeader.VarType.irInt || variable.Count != 1)
                    throw new System.IO.InvalidDataException("Missing iRacing frame identity: " + name);
            }

            SessionInfo = "";
            Is360HzTelemetry = is360HzTelemetry;
			SessionInfoUpdate = sessionUpdate;
			SessionTime = (System.Double)read("SessionTime");
            SessionTick = (System.Int32)read("SessionTick");
            SessionNum = (System.Int32)read("SessionNum");
			SessionState = (CrewChiefV4.iRacing.SessionStates)read("SessionState");
            DisplayUnits = (CrewChiefV4.iRacing.DisplayUnits)read("DisplayUnits");
            SessionFlags = (System.UInt32)read("SessionFlags");
			SessionTimeRemain = (System.Double)read("SessionTimeRemain");
			IsOnTrack = (System.Boolean)read("IsOnTrack");
            IsReplayPlaying = (System.Boolean)read("IsReplayPlaying");
            IsDiskLoggingEnabled = (System.Boolean)read("IsDiskLoggingEnabled");
            IsDiskLoggingActive = (System.Boolean)read("IsDiskLoggingActive");
            PlayerCarPosition = (System.Int32)read("PlayerCarPosition");
			PlayerTrackSurface = (CrewChiefV4.iRacing.TrackSurfaces)read("PlayerTrackSurface");
			PlayerCarIdx = (System.Int32)read("PlayerCarIdx");
			PlayerCarTeamIncidentCount = (System.Int32)read("PlayerCarTeamIncidentCount");
			PlayerCarMyIncidentCount = (System.Int32)read("PlayerCarMyIncidentCount");
			PlayerCarDriverIncidentCount = (System.Int32)read("PlayerCarDriverIncidentCount");
			CarIdxLap = (System.Int32[])read("CarIdxLap");
			CarIdxLapCompleted = (System.Int32[])read("CarIdxLapCompleted");
			CarIdxLapDistPct = (System.Single[])read("CarIdxLapDistPct");
			CarIdxTrackSurface = (CrewChiefV4.iRacing.TrackSurfaces[])read("CarIdxTrackSurface");
			CarIdxOnPitRoad = (System.Boolean[])read("CarIdxOnPitRoad");
			CarIdxPosition = (System.Int32[])read("CarIdxPosition");
			CarIdxClassPosition = (System.Int32[])read("CarIdxClassPosition");
            CarIdxTireCompound = (System.Int32[])read("CarIdxTireCompound");
            CarIdxSessionFlags = (System.Int32[])read("CarIdxSessionFlags");
            OnPitRoad = (System.Boolean)read("OnPitRoad");
            CarIdxSteer = (System.Single[])read("CarIdxSteer");
            CarIdxRPM = (System.Single[])read("CarIdxRPM");
			CarIdxGear = (System.Int32[])read("CarIdxGear");
            CarIdxLastLapTime = (System.Single[])read("CarIdxLastLapTime");
            DRS_Status = (DrsStatus)read("DRS_Status");
            SteeringWheelAngle = (System.Single)read("SteeringWheelAngle");
            Throttle = (System.Single)read("Throttle");
			Brake = (System.Single)read("Brake");
            Clutch = (System.Single)read("Clutch");
            HandBrake = (System.Single)read("HandbrakeRaw");
            Gear = (System.Int32)read("Gear");
            RPM = (System.Single)read("RPM");
            Lap = (System.Int32)read("Lap");
            LapBestLap = (System.Int32)read("LapBestLap");
            LapBestLapTime = (System.Single)read("LapBestLapTime");
            LapLastLapTime = (System.Single)read("LapLastLapTime");
			LapCurrentLapTime = (System.Single)read("LapCurrentLapTime");
			Speed = (System.Single)read("Speed");
			Yaw = (System.Single)read("Yaw");
			Pitch = (System.Single)read("Pitch");
			Roll = (System.Single)read("Roll");
			TrackTempCrew = (System.Single)read("TrackTempCrew");
			AirTemp = (System.Single)read("AirTemp");
			WindVel = (System.Single)read("WindVel");
            Precipitation = (System.Single)read("Precipitation");
            CarLeftRight = (CrewChiefV4.iRacing.CarLeftRight)read("CarLeftRight");
            PitsOpen = (System.Boolean)read("PitsOpen");
			IsInGarage = (System.Boolean)read("IsInGarage");
			EngineWarnings = (CrewChiefV4.iRacing.EngineWarnings)read("EngineWarnings");
            TrackWetness = (CrewChiefV4.iRacing.TrackWetness)read("TrackWetness");
            FuelLevel = (System.Single)read("FuelLevel");
            if(variables.ContainsKey("WaterTemp"))
			    WaterTemp = (System.Single)read("WaterTemp");
            if (variables.ContainsKey("WaterLevel"))
                WaterLevel = (System.Single)read("WaterLevel");
            if (variables.ContainsKey("FuelPress"))
                FuelPress = (System.Single)read("FuelPress");
            if (variables.ContainsKey("OilTemp"))
                OilTemp = (System.Single)read("OilTemp");

			Voltage = (System.Single)read("Voltage");
			RRcoldPressure = (System.Single)read("RRcoldPressure");
			LRcoldPressure = (System.Single)read("LRcoldPressure");
			RFcoldPressure = (System.Single)read("RFcoldPressure");
			LFcoldPressure = (System.Single)read("LFcoldPressure");

            LFtempCL = (Single)read("LFtempCL");
            LFtempCM = (Single)read("LFtempCM");
            LFtempCR = (Single)read("LFtempCR");
            LFwearL = (Single)read("LFwearL");
            LFwearM = (Single)read("LFwearM");
            LFwearR = (Single)read("LFwearR");
            RFtempCL = (Single)read("RFtempCL");
            RFtempCM = (Single)read("RFtempCM");
            RFtempCR = (Single)read("RFtempCR");
            RFwearL = (Single)read("RFwearL");
            RFwearM = (Single)read("RFwearM");
            RFwearR = (Single)read("RFwearR");
            LRtempCL = (Single)read("LRtempCL");
            LRtempCM = (Single)read("LRtempCM");
            LRtempCR = (Single)read("LRtempCR");
            LRwearL = (Single)read("LRwearL");
            LRwearM = (Single)read("LRwearM");
            LRwearR = (Single)read("LRwearR");
            RRtempCL = (Single)read("RRtempCL");
            RRtempCM = (Single)read("RRtempCM");
            RRtempCR = (Single)read("RRtempCR");
            RRwearL = (Single)read("RRwearL");
            RRwearM = (Single)read("RRwearM");
            RRwearR = (Single)read("RRwearR");

            if (Is360HzTelemetry)
            {
                _VertAccel = (System.Single[])read("VertAccel");
                _LatAccel = (System.Single[])read("LatAccel");
                _LongAccel = (System.Single[])read("LongAccel");

                /*
                _RRshockDefl = (System.Single[])sdk.GetData("RRshockDefl");
                _RRshockVel = (System.Single[])sdk.GetData("RRshockVel");

                _LRshockDefl = (System.Single[])sdk.GetData("LRshockDefl");
                _LRshockVel = (System.Single[])sdk.GetData("LRshockVel");

                _RFshockDefl = (System.Single[])sdk.GetData("RFshockDefl");
                _RFshockVel = (System.Single[])sdk.GetData("RFshockVel");  
                
                _LFshockDefl = (System.Single[])sdk.GetData("LFshockDefl");
                _LFshockVel = (System.Single[])sdk.GetData("LFshockVel");
                */
            }
            else
            {
                _VertAccel = new System.Single[1];
                _VertAccel[0] =  (System.Single)read("VertAccel");
                _LatAccel = new System.Single[1];
                _LatAccel[0] = (System.Single)read("LatAccel");
                _LongAccel = new System.Single[1];
                _LongAccel[0] = (System.Single)read("LongAccel");

                /*
                _RRshockDefl = new System.Single[1];
                _RRshockDefl[0] = (System.Single)sdk.GetData("RRshockDefl");
                _RRshockVel = new System.Single[1];
                _RRshockVel[0] = (System.Single)sdk.GetData("RRshockVel");

                _LRshockDefl = new System.Single[1];
                _LRshockDefl[0] = (System.Single)sdk.GetData("LRshockDefl");
                _LRshockVel = new System.Single[1];
                _LRshockVel[0] = (System.Single)sdk.GetData("LRshockVel");

                _RFshockDefl = new System.Single[1];
                _RFshockDefl[0] = (System.Single)sdk.GetData("RFshockDefl");
                _RFshockVel = new System.Single[1];
                _RFshockVel[0] = (System.Single)sdk.GetData("RFshockVel");

                _LFshockDefl = new System.Single[1];
                _LFshockDefl[0] = (System.Single)sdk.GetData("LFshockDefl");
                _LFshockVel = new System.Single[1];
                _LFshockVel[0] = (System.Single)sdk.GetData("LFshockVel");
                */

            }
            PitSvFlags = (CrewChiefV4.iRacing.PitServiceFlags)read("PitSvFlags");
            PitSvLFP = (System.Single)read("PitSvLFP");
            PitSvRFP = (System.Single)read("PitSvRFP");
            PitSvLRP = (System.Single)read("PitSvLRP");
            PitSvRRP = (System.Single)read("PitSvRRP");
            PitSvFuel = (System.Single)read("PitSvFuel");
            PitRepairLeft = (System.Single)read("PitRepairLeft");
            PitOptRepairLeft = (System.Single)read("PitOptRepairLeft");
            PlayerCarTowTime = (System.Single)read("PlayerCarTowTime");
            PlayerCarInPitStall = (System.Boolean)read("PlayerCarInPitStall");
            PlayerCarPitSvStatus = (CrewChiefV4.iRacing.PitSvStatus)read("PlayerCarPitSvStatus");
            PlayerTireCompound = (System.Int32)read("PlayerTireCompound");
            PitSvTireCompound = (System.Int32)read("PitSvTireCompound");

            // Fast Repairs: -1 = this build of the sim does not provide the value.
            FastRepairUsed = variables.ContainsKey("FastRepairUsed") ? (System.Int32)read("FastRepairUsed") : -1;
            FastRepairAvailable = variables.ContainsKey("FastRepairAvailable") ? (System.Int32)read("FastRepairAvailable") : -1;
            // RRCA 0.41: Art des letzten Vorfalls (Mauer, Auto, neben der Strecke); liegt nur einen Takt an.
            PlayerIncidents = variables.ContainsKey("PlayerIncidents") ? (System.Int32)read("PlayerIncidents") : 0;

            PaceMode = (PaceMode)read("PaceMode");
            CarIdxPaceLine = (System.Int32[])read("CarIdxPaceLine");
            CarIdxPaceRow = (System.Int32[])read("CarIdxPaceRow");
            CarIdxPaceFlags = (PaceFlags[])read("CarIdxPaceFlags");

            if (variables.ContainsKey("dcBrakeBias"))
            {
                dcBrakeBias = (Single)read("dcBrakeBias");
            }
        }
        public iRacingData() {}
		public System.Boolean IsNewSession;
		public System.Int32 SessionInfoUpdate;
		public System.String SessionInfo;
        public System.Boolean Is360HzTelemetry; 
		/// <summary>
		/// Seconds since session start
		/// <summary>
		public System.Double SessionTime;
        
        /// <summary>
        /// Current update number
        /// <summary>
        public System.Int32 SessionTick;

        /// <summary>
        /// Session number
        /// <summary>
        public System.Int32 SessionNum;

		/// <summary>
		/// Session state
		/// <summary>
		public CrewChiefV4.iRacing.SessionStates SessionState;

        public CrewChiefV4.iRacing.DisplayUnits DisplayUnits;

		/// <summary>
		/// Session flags
		/// <summary>
		public System.UInt32 SessionFlags;

		/// <summary>
		/// Seconds left till session ends
		/// <summary>
		public System.Double SessionTimeRemain;

		/// <summary>
		/// 1=Car on track physics running with player in car
		/// <summary>
		public System.Boolean IsOnTrack;

        /// <summary>
        /// 0=replay not playing  1=replay playing
        /// <summary>
        public System.Boolean IsReplayPlaying;

        /// <summary>
        /// 0=disk based telemetry turned off  1=turned on
        /// <summary>
        public System.Boolean IsDiskLoggingEnabled;

        /// <summary>
        /// 0=disk based telemetry file not being written  1=being written
        /// <summary>
        public System.Boolean IsDiskLoggingActive;
        /// <summary>
        /// Players position in race
        /// <summary>
        public System.Int32 PlayerCarPosition;

		/// <summary>
		/// Players car track surface type
		/// <summary>
		public CrewChiefV4.iRacing.TrackSurfaces PlayerTrackSurface;

		/// <summary>
		/// Players carIdx
		/// <summary>
		public System.Int32 PlayerCarIdx;

		/// <summary>
		/// Players team incident count for this session
		/// <summary>
		public System.Int32 PlayerCarTeamIncidentCount;

		/// <summary>
		/// Players own incident count for this session
		/// <summary>
		public System.Int32 PlayerCarMyIncidentCount;

		/// <summary>
		/// Teams current drivers incident count for this session
		/// <summary>
		public System.Int32 PlayerCarDriverIncidentCount;

        /// <summary>
        /// Players car current tire compound
        /// <summary>
        public System.Int32 PlayerTireCompound;

        /// <summary>
        /// Laps started by car index
        /// <summary>
        public System.Int32[] CarIdxLap;

		/// <summary>
		/// Laps completed by car index
		/// <summary>
		public System.Int32[] CarIdxLapCompleted;

		/// <summary>
		/// Percentage distance around lap by car index
		/// <summary>
		public System.Single[] CarIdxLapDistPct;

		/// <summary>
		/// Track surface type by car index
		/// <summary>
		public CrewChiefV4.iRacing.TrackSurfaces[] CarIdxTrackSurface;

		/// <summary>
		/// Track surface material type by car index
		/// <summary>
		public CrewChiefV4.iRacing.TrackSurfaceMaterial[] CarIdxTrackSurfaceMaterial;

		/// <summary>
		/// On pit road between the cones by car index
		/// <summary>
		public System.Boolean[] CarIdxOnPitRoad;

		/// <summary>
		/// Cars position in race by car index
		/// <summary>
		public System.Int32[] CarIdxPosition;

		/// <summary>
		/// Cars class position in race by car index
		/// <summary>
		public System.Int32[] CarIdxClassPosition;

        /// <summary>
        /// Session flags for each player
        /// <summary>
        public System.Int32[] CarIdxSessionFlags;

        /// <summary>
        /// Cars current tire compound
        /// <summary>
        public System.Int32[] CarIdxTireCompound;

        /// <summary>
        /// Is the player car on pit road between the cones
        /// <summary>
        public System.Boolean OnPitRoad;

        /// <summary>
        /// Steering wheel angle by car index
        /// <summary
        public System.Single[] CarIdxSteer;
        /// <summary>
        /// Engine rpm by car index
        /// <summary>
        public System.Single[] CarIdxRPM;

		/// <summary>
		/// -1=reverse  0=neutral  1..n=current gear by car index
		/// <summary>
		public System.Int32[] CarIdxGear;

        /// <summary>
        /// Cars last lap time
        /// <summary>

        public System.Single[] CarIdxLastLapTime;

        /// <summary>
        /// 0=no drs available, 1=drs detected, 2=drs available, 3=drs enabled
        /// <summary>
        public DrsStatus DRS_Status;

        /// <summary>
        /// Steering wheel angle
        /// <summary>
        public System.Single SteeringWheelAngle;
        /// <summary>
        /// 0=off throttle to 1=full throttle
        /// <summary>
        public System.Single Throttle;

		/// <summary>
		/// 0=brake released to 1=max pedal force
		/// <summary>
		public System.Single Brake;

        /// <summary>
        /// 0=brake released to 1=max force
        /// <summary>
        public System.Single HandBrake;

        /// <summary>
        /// 0=disengaged to 1=fully engaged
        /// <summary>
        public System.Single Clutch;

		/// <summary>
		/// -1=reverse  0=neutral  1..n=current gear
		/// <summary>
		public System.Int32 Gear;

        /// <summary>
        /// Engine rpm
        /// <summary>
        public System.Single RPM;

        /// <summary>
        /// Laps started count
        /// <summary>
        public System.Int32 Lap;

        /// <summary>
        /// Players best lap time
        /// <summary>
        public System.Single LapBestLapTime;

		/// <summary>
		/// Players last lap time
		/// <summary>
		public System.Single LapLastLapTime;

        /// <summary>
        /// Players best lap number
        /// <summary>
        public System.Int32 LapBestLap;

        /// <summary>
        /// Estimate of players current lap time as shown in F3 box
        /// <summary>
        public System.Single LapCurrentLapTime;

		/// <summary>
		/// GPS vehicle speed
		/// <summary>
		public System.Single Speed;

		/// <summary>
		/// Yaw orientation
		/// <summary>
		public System.Single Yaw;

		/// <summary>
		/// Pitch orientation
		/// <summary>
		public System.Single Pitch;

		/// <summary>
		/// Roll orientation
		/// <summary>
		public System.Single Roll;

		/// <summary>
		/// Temperature of track measured by crew around track
		/// <summary>
		public System.Single TrackTempCrew;

		/// <summary>
		/// Temperature of air at start/finish line
		/// <summary>
		public System.Single AirTemp;

		/// <summary>
		/// Wind velocity at start/finish line
		/// <summary>
		public System.Single WindVel;

        /// <summary>
        /// Precipitation at start/finish line
        /// <summary>
        public System.Single Precipitation;

        public CrewChiefV4.iRacing.TrackWetness TrackWetness;

        /// <summary>
        /// Notify if car is to the left or right of driver
        /// <summary>
        public CrewChiefV4.iRacing.CarLeftRight CarLeftRight;

        /// <summary>
        /// True if pit stop is allowed for the current player
        /// <summary>
        public System.Boolean PitsOpen;

		/// <summary>
		/// 1=Car in garage physics running
		/// <summary>
		public System.Boolean IsInGarage;

		/// <summary>
		/// Bitfield for warning lights
		/// <summary>
		public CrewChiefV4.iRacing.EngineWarnings EngineWarnings;

		/// <summary>
		/// Liters of fuel remaining
		/// <summary>
		public System.Single FuelLevel;

		/// <summary>
		/// Engine coolant temp
		/// <summary>
		public System.Single WaterTemp;

		/// <summary>
		/// Engine coolant level
		/// <summary>
		public System.Single WaterLevel;

		/// <summary>
		/// Engine fuel pressure
		/// <summary>
		public System.Single FuelPress;

		/// <summary>
		/// Engine oil temperature
		/// <summary>
		public System.Single OilTemp;

		/// <summary>
		/// Engine voltage
		/// <summary>
		public System.Single Voltage;

		/// <summary>
		/// RR tire cold pressure  as set in the garage
		/// <summary>
		public System.Single RRcoldPressure;

		/// <summary>
		/// LR tire cold pressure  as set in the garage
		/// <summary>
		public System.Single LRcoldPressure;

		/// <summary>
		/// RF tire cold pressure  as set in the garage
		/// <summary>
		public System.Single RFcoldPressure;

		/// <summary>
		/// LF tire cold pressure  as set in the garage
		/// <summary>
		public System.Single LFcoldPressure;

        /// <summary>
        /// Vertical acceleration (including gravity)
        private System.Single[] _VertAccel;
        public System.Single VertAccel
        {
            get
            {
                return _VertAccel == null ? 0 : _VertAccel.Average();
            }                                
        }

        /// <summary>
        /// Lateral acceleration (including gravity)
        /// <summary>
        private System.Single[] _LatAccel;
        public System.Single LatAccel
        {
            get
            {
                return _LatAccel == null ? 0 : _LatAccel.Average();
            }
        }

        /// <summary>
        /// Longitudinal acceleration (including gravity)
        /// <summary>
        private System.Single[] _LongAccel;       
        public System.Single LongAccel
        {
            get
            {
                return _LongAccel == null ? 0 : _LongAccel.Average();
            }
        }

        /// <summary>
        /// Bitfield of pit service checkboxes
        /// <summary>
        public CrewChiefV4.iRacing.PitServiceFlags PitSvFlags;

        /// <summary>
        /// Pit service left front tire pressure
        /// <summary>
        public System.Single PitSvLFP;

        /// <summary>
        /// Pit service right front tire pressure
        /// <summary>
        public System.Single PitSvRFP;

        /// <summary>
        /// Pit service left rear tire pressure
        /// <summary>
        public System.Single PitSvLRP;

        /// <summary>
        /// Pit service right rear tire pressure
        /// <summary>
        public System.Single PitSvRRP;

        /// <summary>
        /// Pit service fuel add amount
        /// <summary>
        public System.Single PitSvFuel;
        
        /// <summary>
        /// Time left for mandatory pit repairs if repairs are active
        /// <summary>
        public System.Single PitRepairLeft;

        /// <summary>
        /// Time left for optional repairs if repairs are active
        /// <summary>
        public System.Single PitOptRepairLeft;

        /// <summary>
        /// Pit service pending tire compound
        /// <summary>
        public System.Int32 PitSvTireCompound;

        /// <summary>
        /// Players car is being towed if time is greater than zero
        /// <summary>
        public System.Single PlayerCarTowTime;

        /// <summary>
        /// Players car is properly in there pitstall
        /// <summary>
        public System.Boolean PlayerCarInPitStall;

        /// <summary>
        /// Players car pit service status bits
        /// <summary>
        public CrewChiefV4.iRacing.PitSvStatus PlayerCarPitSvStatus;

        /// <summary>
        /// RR shock deflection
        /// <summary>
        private System.Single[] _RRshockDefl;
        public System.Single RRshockDefl
        {
            get
            {
                return _RRshockDefl == null ? 0 : _RRshockDefl.Average();
            }
        }
        /// <summary>
        /// RR shock velocity
        /// <summary>
        private System.Single[] _RRshockVel;
        public System.Single RRshockVel
        {
            get
            {
                return _RRshockVel == null ? 0 : _RRshockVel.Average();
            }
        }
        /// <summary>
        /// LR shock deflection
        /// <summary>
        private System.Single[] _LRshockDefl;
        public System.Single LRshockDefl
        {
            get
            {
                return _LRshockDefl == null ? 0 : _LRshockDefl.Average();
            }
        }
        /// <summary>
        /// LR shock velocity
        /// <summary>
        private System.Single[] _LRshockVel;
        public System.Single LRshockVel
        {
            get
            {
                return _LRshockVel == null ? 0 : _LRshockVel.Average();
            }
        }
        /// <summary>
        /// RF shock deflection
        /// <summary>
        private System.Single[] _RFshockDefl;
        public System.Single RFshockDefl
        {
            get
            {
                return _RFshockDefl == null ? 0 : _RFshockDefl.Average();
            }
        }
        /// <summary>
        /// RF shock velocity
        /// <summary>
        private System.Single[] _RFshockVel;
        public System.Single RFshockVel
        {
            get
            {
                return _RFshockVel == null ? 0 : _RFshockVel.Average();
            }
        }
        /// <summary>
        /// LF shock deflection
        /// <summary>
        private System.Single[] _LFshockDefl;
        public System.Single LFshockDefl
        {
            get
            {
                return _LFshockDefl == null ? 0 : _LFshockDefl.Average();
            }
        }
        /// <summary>
        /// LF shock velocity
        /// <summary>
        private System.Single[] _LFshockVel;
        public System.Single LFshockVel
        {
            get
            {
                return _LFshockVel == null ? 0 : _LFshockVel.Average();
            }
        }


        // the tires temps / wear only show up in the pit stall (if at all)
        // and (as of 2024-03-18) there's no way to get pressure or surface
        // temps from the realtime telemetry.
        //
        // brake line pressure could be used to realtime monitor if brake bias needs to be
        // adjusted, but it seems to be all zeros. Perhaps iRacing redacted it after realising.
        //
        // tyre speed can be used to detect locking and spinning of wheels, which can lead
        // to advice on driving style and / or TC / ABS settings changes. However, it is not
        // available in the realtime telemetry (not present rather than all zeros), despite
        // being (incorrectly) documented in the iRacing telemetry handbook to be there.
        // We could use the engine RPM + gear to estimate "normal" driven wheel speed as
        // a workaround, but then we need to know which weels are the driven ones.
        public Single LFtempCL;
        public Single LFtempCM;
        public Single LFtempCR;
        public Single LFwearL;
        public Single LFwearM;
        public Single LFwearR;
        public Single RFtempCL;
        public Single RFtempCM;
        public Single RFtempCR;
        public Single RFwearL;
        public Single RFwearM;
        public Single RFwearR;
        public Single LRtempCL;
        public Single LRtempCM;
        public Single LRtempCR;
        public Single LRwearL;
        public Single LRwearM;
        public Single LRwearR;
        public Single RRtempCL;
        public Single RRtempCM;
        public Single RRtempCR;
        public Single RRwearL;
        public Single RRwearM;
        public Single RRwearR;

        // these variants all the return the minimum tread from the 3 measurements
        // (these are called "wear" but should be called "tread remaining")
        public Single LFwear()
        {
            return Math.Min(LFwearL, Math.Min(LFwearM, LFwearR));
        }
        public Single RFwear()
        {
            return Math.Min(RFwearL, Math.Min(RFwearM, RFwearR));
        }
        public Single LRwear()
        {
            return Math.Min(LRwearL, Math.Min(LRwearM, LRwearR));
        }
        public Single RRwear()
        {
            return Math.Min(RRwearL, Math.Min(RRwearM, RRwearR));
        }

        /// <summary>
        /// Are we pacing or not
        /// <summary>
        public System.Int32 FastRepairUsed = -1;
        public System.Int32 FastRepairAvailable = -1;
        /// <summary>irsdk_IncidentFlags: untere 8 Bit Art (2 neben der Strecke, 4/5 Begrenzung, 7/8 Auto), darüber die Punkte.</summary>
        public System.Int32 PlayerIncidents;
        public PaceMode PaceMode;

        /// <summary>
        /// What line cars are pacing in  or -1 if not pacing
        /// This updates incrementally to indicate where we should go next (not a final destination).
        /// Zero means the same side as the driver in pole.
        /// <summary>
        public System.Int32[] CarIdxPaceLine;

        // inferred using the definition of 0 from (re)start_on_left
        public GameState.FrozenOrderColumn[] CarIdxPaceColumn(bool pole_on_left)
        {
            var columns = new GameState.FrozenOrderColumn[CarIdxPaceLine.Length];
            for (int i = 0; i < CarIdxPaceLine.Length; i++)
            {
                if (CarIdxPaceLine[i] == 0)
                {
                    columns[i] = pole_on_left ? GameState.FrozenOrderColumn.Left : GameState.FrozenOrderColumn.Right;
                }
                else if (CarIdxPaceLine[i] == 1)
                {
                    columns[i] = pole_on_left ? GameState.FrozenOrderColumn.Right : GameState.FrozenOrderColumn.Left;
                }
                else
                {
                    columns[i] = GameState.FrozenOrderColumn.None;
                }
            }
            return columns;
        }

        /// <summary>
        /// What row cars are pacing in  or -1 if not pacing
        /// This updates incrementally to indicate where we should go next (not a final destination).
        /// <summary>
        public System.Int32[] CarIdxPaceRow;

        /// <summary>
        /// Pacing status flags for each car
        /// <summary>
        public PaceFlags[] CarIdxPaceFlags;

        /// <summary>
        /// In car brake bias, if available.
        /// </summary>
        public Single dcBrakeBias;
    }
}
