using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace CrewChiefV4.AMS2
{
    public class StructHelper
    {
        public static Encoding ENCODING = Encoding.UTF8;
                   
        static StructHelper() {
            try {
                ENCODING = Encoding.GetEncoding(UserSettings.GetUserSettings().getString("ams2_character_encoding"));
            }
            catch (System.ArgumentException)
            {
                Console.WriteLine("Using default encoding");
                ENCODING = Encoding.Default;
            }
        }
        public static ams2APIStruct Clone<ams2APIStruct>(ams2APIStruct ams2Struct)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, ams2Struct);
                ms.Position = 0;
                return (ams2APIStruct)formatter.Deserialize(ms);
            }
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sTelemetryData udpTelemetryData)
        {
            // Participant Info
            existingState.mViewedParticipantIndex = udpTelemetryData.sViewedParticipantIndex;

            // Unfiltered Input
            existingState.mUnfilteredThrottle = (float)udpTelemetryData.sUnfilteredThrottle / 255f;
            existingState.mUnfilteredBrake = (float)udpTelemetryData.sUnfilteredBrake / 255f;
            existingState.mUnfilteredSteering = (float)udpTelemetryData.sUnfilteredSteering / 127f;
            existingState.mUnfilteredClutch = (float)udpTelemetryData.sUnfilteredClutch / 255f;

            // Car State
            existingState.mCarFlags = udpTelemetryData.sCarFlags;
            existingState.mOilTempCelsius = udpTelemetryData.sOilTempCelsius; 
            existingState.mOilPressureKPa = udpTelemetryData.sOilPressureKPa; 
            existingState.mWaterTempCelsius = udpTelemetryData.sWaterTempCelsius; 
            existingState.mWaterPressureKPa = udpTelemetryData.sWaterPressureKpa;
            existingState.mFuelPressureKPa = udpTelemetryData.sFuelPressureKpa;
            existingState.mFuelLevel = udpTelemetryData.sFuelLevel;
            existingState.mFuelCapacity = udpTelemetryData.sFuelCapacity; 
            existingState.mSpeed = udpTelemetryData.sSpeed; 
            existingState.mMaxRPM = udpTelemetryData.sMaxRpm;
            existingState.mBrake = (float)udpTelemetryData.sBrake / 255f;
            existingState.mHandBrake = (float)udpTelemetryData.sHandBrake / 255f;
            existingState.mThrottle = (float)udpTelemetryData.sThrottle / 255f;
            existingState.mClutch = (float)udpTelemetryData.sClutch / 255f;
            existingState.mSteering = (float)udpTelemetryData.sSteering / 127f;
            existingState.mGear = udpTelemetryData.sGearNumGears & 15;
            existingState.mNumGears = udpTelemetryData.sGearNumGears >> 4;
            existingState.mOdometerKM = udpTelemetryData.sOdometerKM;                             
            existingState.mBoostAmount = udpTelemetryData.sBoostAmount;

            // Motion & Device Related
            existingState.mOrientation = udpTelemetryData.sOrientation; 
            existingState.mLocalVelocity = udpTelemetryData.sLocalVelocity;
            existingState.mWorldVelocity = udpTelemetryData.sWorldVelocity;
            existingState.mAngularVelocity = udpTelemetryData.sAngularVelocity;
            existingState.mLocalAcceleration = udpTelemetryData.sLocalAcceleration;
            existingState.mWorldAcceleration = udpTelemetryData.sWorldAcceleration;
            existingState.mExtentsCentre = udpTelemetryData.sExtentsCentre; 


            existingState.mTyreFlags = toUIntArray(udpTelemetryData.sTyreFlags); 
            existingState.mTerrain = toUIntArray(udpTelemetryData.sTerrain);
            existingState.mTyreY = udpTelemetryData.sTyreY;
            existingState.mTyreRPS = udpTelemetryData.sTyreRPS;
            existingState.mTyreTemp = toFloatArray(udpTelemetryData.sTyreTemp, 255); 
            existingState.mTyreHeightAboveGround = udpTelemetryData.sTyreHeightAboveGround;
            existingState.mTyreWear = toFloatArray(udpTelemetryData.sTyreWear, 255); 
            existingState.mBrakeTempCelsius = toFloatArray(udpTelemetryData.sBrakeTempCelsius, 1);
            existingState.mTyreTreadTemp = toFloatArray(udpTelemetryData.sTyreTreadTemp, 1);            
            existingState.mTyreLayerTemp = toFloatArray(udpTelemetryData.sTyreLayerTemp, 1); 
            existingState.mTyreCarcassTemp = toFloatArray(udpTelemetryData.sTyreCarcassTemp, 1); 
            existingState.mTyreRimTemp = toFloatArray(udpTelemetryData.sTyreRimTemp, 1);    
            existingState.mTyreInternalAirTemp = toFloatArray(udpTelemetryData.sTyreInternalAirTemp, 1);

            existingState.mWheelLocalPositionY = udpTelemetryData.sWheelLocalPositionY;
            existingState.mRideHeight = udpTelemetryData.sRideHeight;
            existingState.mSuspensionTravel = udpTelemetryData.sSuspensionTravel;
            existingState.mSuspensionVelocity = udpTelemetryData.sSuspensionVelocity;
            existingState.mAirPressure = toFloatArray(udpTelemetryData.sAirPressure, 1);

            existingState.mEngineSpeed = udpTelemetryData.sEngineSpeed;
            existingState.mEngineTorque = udpTelemetryData.sEngineTorque;

            // Car Damage
            existingState.mCrashState = udpTelemetryData.sCrashState;
            existingState.mAeroDamage = (float)udpTelemetryData.sAeroDamage / 255f;
            existingState.mEngineDamage = (float)udpTelemetryData.sEngineDamage / 255f;
            existingState.mBrakeDamage = toFloatArray(udpTelemetryData.sBrakeDamage, 255);
            existingState.mSuspensionDamage = toFloatArray(udpTelemetryData.sSuspensionDamage, 255);    

            existingState.mWings = toFloatArray(udpTelemetryData.sWings, 1);

            existingState.mJoyPad0 = udpTelemetryData.sJoyPad1;
            existingState.mDPad = udpTelemetryData.sDPad;

            // tyres
            existingState.mLFTyreCompoundName = udpTelemetryData.lfTyreCompound;
            existingState.mRFTyreCompoundName = udpTelemetryData.rfTyreCompound;
            existingState.mLRTyreCompoundName = udpTelemetryData.lrTyreCompound;
            existingState.mRRTyreCompoundName = udpTelemetryData.rrTyreCompound;
            
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sRaceData raceData)
        {
            Boolean isTimedSession = (raceData.sLapsTimeInEvent >> 15) == 1;
            int sessionLength = raceData.sLapsTimeInEvent / 2;  // need the bottom bits of this short here - is it valid to just halve it?
            if (isTimedSession)
            {
                existingState.mLapsInEvent = (uint) sessionLength;
                existingState.mSessionDuration = 0;
            }
            else
            {
                existingState.mSessionDuration = 300 * (uint) sessionLength; // *300 because this is in 5 minutes blocks
                existingState.mLapsInEvent = 0;
            }
            existingState.mTrackLength = raceData.sTrackLength;

            existingState.mWorldFastestLapTime = raceData.sWorldFastestLapTime;
            existingState.mWorldFastestSector1Time = raceData.sWorldFastestSector1Time;
            existingState.mWorldFastestSector2Time = raceData.sWorldFastestSector2Time;
            existingState.mWorldFastestSector3Time = raceData.sWorldFastestSector3Time;
            existingState.mPersonalFastestLapTime = raceData.sPersonalFastestLapTime;
            existingState.mPersonalFastestSector1Time = raceData.sPersonalFastestSector1Time;
            existingState.mPersonalFastestSector2Time = raceData.sPersonalFastestSector2Time;
            existingState.mPersonalFastestSector3Time = raceData.sPersonalFastestSector3Time;

            existingState.mTrackLocation = raceData.sTrackLocation;
            existingState.mTrackVariation = raceData.sTrackVariation;
            existingState.mTranslatedTrackLocation = raceData.sTranslatedTrackLocation;
            existingState.mTranslatedTrackVariation = raceData.sTranslatedTrackVariation;

            existingState.mEnforcedPitStopLap = raceData.sEnforcedPitStopLap;
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sParticipantsData participantsData)
        {
            if (existingState.mParticipantData == null)
            {
                existingState.mParticipantData = new ams2APIParticipantStruct[32];
            }
            int offset = (participantsData.mPartialPacketIndex -1) * 16;
            // existingState is a struct, so any changes we make as we iterate this array will be done to a copy, not a reference
            for (int i = offset; i < offset + 16 && i < existingState.mParticipantData.Length; i++)
            {
                ams2APIParticipantStruct existingParticipant = existingState.mParticipantData[i];
                existingParticipant.mName = participantsData.sName[i - offset].nameByteArray;
                existingState.mParticipantData[i] = existingParticipant;
            }
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sTimingsData timingsData)
        {
            existingState.mNumParticipants = timingsData.sNumParticipants;   
            existingState.mEventTimeRemaining = timingsData.sEventTimeRemaining;// time remaining, -1 for invalid time,  -1 - laps remaining in lap based races  --
            existingState.mSplitTimeAhead = timingsData.sSplitTimeAhead;
            existingState.mSplitTimeBehind = timingsData.sSplitTimeBehind;
            existingState.mSplitTime = timingsData.sSplitTime;  // what's this?
            if (existingState.mParticipantData == null)
            {
                existingState.mParticipantData = new ams2APIParticipantStruct[32];
            }
            if (existingState.mRaceStates == null)
            {
                existingState.mRaceStates = new uint[32];
            }
            if (existingState.mLapsInvalidated == null)
            {
                existingState.mLapsInvalidated = new byte[32];
            }
            if (existingState.mPitModes == null)
            {
                existingState.mPitModes = new uint[32];
            }
            if (existingState.mCurrentSector1Times == null)
            {
                existingState.mCurrentSector1Times = new float[32];
            }
            if (existingState.mCurrentSector2Times == null)
            {
                existingState.mCurrentSector2Times = new float[32];
            }
            if (existingState.mCurrentSector3Times == null)
            {
                existingState.mCurrentSector3Times = new float[32];
            }
            for (int i = 0; i < existingState.mParticipantData.Length; i++)
            {
                sParticipantInfo newParticipantInfo = timingsData.sParticipants[i];
                Boolean isHuman = (newParticipantInfo.sCarIndex >> 7) == 1;
                uint carIndex = (uint)newParticipantInfo.sCarIndex & 127;

                Boolean isActive = (newParticipantInfo.sRacePosition >> 7) == 1;
                ams2APIParticipantStruct existingPartInfo = existingState.mParticipantData[i];

                if (isActive)
                {
                    existingPartInfo.mIsActive = i < existingState.mNumParticipants;

                    existingPartInfo.mCurrentLap = newParticipantInfo.sCurrentLap;
                    // is this safe?:
                    existingPartInfo.mLapsCompleted = existingPartInfo.mCurrentLap - 1;
                    existingPartInfo.mCurrentLapDistance = newParticipantInfo.sCurrentLapDistance;
                    existingPartInfo.mRacePosition = (uint)newParticipantInfo.sRacePosition & 127;
                    existingPartInfo.mCurrentSector = newParticipantInfo.sSector & 7;

                    // err... laps completed is missing?
                    // existingPartInfo.mLapsCompleted = (uint)newParticipantInfo.sLapsCompleted & 127;
                    byte lapInvalidated = (byte)(newParticipantInfo.sRaceState >> 7);
                    existingState.mRaceStates[i] = (uint)newParticipantInfo.sRaceState & 127;
                    if (i == existingState.mViewedParticipantIndex)
                    {
                        existingState.mRaceState = existingState.mRaceStates[i];
                    }
                    existingState.mLapsInvalidated[i] = lapInvalidated;
                    existingState.mPitModes[i] = (uint) newParticipantInfo.sPitModeSchedule & 28;
                    existingState.mPitSchedules[i] = (uint) newParticipantInfo.sPitModeSchedule & 3;
                    existingState.mHighestFlagColours[i] = (uint)newParticipantInfo.sHighestFlag & 28;
                    existingState.mHighestFlagReasons[i] = (uint)newParticipantInfo.sHighestFlag & 3;
                    // no obvious slot in MMF for currentTime - do we need it if we have currentsectortime for S3?
                    if (existingPartInfo.mCurrentSector == 1)
                    {
                        existingState.mCurrentSector1Times[i] = newParticipantInfo.sCurrentSectorTime;
                    }
                    if (existingPartInfo.mCurrentSector == 2)
                    {
                        existingState.mCurrentSector2Times[i] = newParticipantInfo.sCurrentSectorTime;
                    }
                    if (existingPartInfo.mCurrentSector == 3)
                    {
                        existingState.mCurrentSector3Times[i] = newParticipantInfo.sCurrentSectorTime;
                    }
                    
                    // and now the bit magic for the extra position precision...
                    float[] newWorldPositions = toFloatArray(newParticipantInfo.sWorldPosition, 1);
                    float xAdjustment = ((float)((uint)newParticipantInfo.sSector >> 6 & 3)) / 4f;
                    float zAdjustment = ((float)((uint)newParticipantInfo.sSector >> 4 & 3)) / 4f;

                    newWorldPositions[0] = newWorldPositions[0] + xAdjustment;
                    newWorldPositions[2] = newWorldPositions[2] + zAdjustment;
                    existingPartInfo.mWorldPosition = newWorldPositions;

                    if (i == existingState.mViewedParticipantIndex)
                    {
                        existingState.mLapInvalidated = lapInvalidated == 1;
                        existingState.mHighestFlagColour = existingState.mHighestFlagColours[i];
                        existingState.mHighestFlagReason = existingState.mHighestFlagReasons[i];
                        existingState.mPitMode = existingState.mPitModes[i];
                        existingState.mPitSchedule = existingState.mPitSchedules[i];
                    }
                }
                else
                {
                    existingPartInfo.mWorldPosition = new float[] { 0, 0, 0 };
                    existingPartInfo.mIsActive = false;
                }
                existingState.mParticipantData[i] = existingPartInfo;
            }
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sGameStateData gameStateData)
        {
            existingState.mGameState = (uint)gameStateData.mGameState & 7;
            existingState.mSessionState = (uint)gameStateData.mGameState>> 4;
            existingState.mAmbientTemperature = gameStateData.sAmbientTemperature;
            existingState.mTrackTemperature = gameStateData.sTrackTemperature;
            existingState.mRainDensity = gameStateData.sRainDensity;
            existingState.mSnowDensity = gameStateData.sSnowDensity;
            existingState.mWindSpeed = gameStateData.sWindSpeed;
            existingState.mWindDirectionX = gameStateData.sWindDirectionX;
            existingState.mWindDirectionY = gameStateData.sWindDirectionY;
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sTimeStatsData timeStatsData)
        {
            float[] lastSectorTimes = new float[32];
            if (existingState.mFastestLapTimes == null)
            {
                existingState.mFastestLapTimes = new float[32];
            }
            if (existingState.mFastestSector1Times == null)
            {
                existingState.mFastestSector1Times = new float[32];
            }
            if (existingState.mFastestSector2Times == null)
            {
                existingState.mFastestSector2Times = new float[32];
            }
            if (existingState.mFastestSector3Times == null)
            {
                existingState.mFastestSector3Times = new float[32];
            }
            for (int i = 0; i < 32; i++)
            {
                sParticipantStatsInfo participantInfo = timeStatsData.sStats.sParticipants[i];
                existingState.mFastestLapTimes[i] = participantInfo.sFastestLapTime;
                existingState.mFastestSector1Times[i] = participantInfo.sFastestSector1Time;
                existingState.mFastestSector2Times[i] = participantInfo.sFastestSector2Time;
                existingState.mFastestSector3Times[i] = participantInfo.sFastestSector3Time;
                existingState.mLastLapTime = participantInfo.sLastLapTime;
                lastSectorTimes[i] = participantInfo.sLastSectorTime;
                if (i == existingState.mViewedParticipantIndex)
                {
                    existingState.mLastLapTime = participantInfo.sLastLapTime;
                }
            }
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sParticipantVehicleNamesData participantVehicleNamesData)
        {
            int offset = (participantVehicleNamesData.mPartialPacketIndex - 1) * 16;
            if (existingState.mCarNames == null)
            {
                existingState.mCarNames = new byte[64*64];
            }
            for (int i = 0; i < 16; i++)
            {
                ushort index = participantVehicleNamesData.sVehicles[i].sIndex;
                uint classIndex = participantVehicleNamesData.sVehicles[i].sClass;
                byte[] name = participantVehicleNamesData.sVehicles[i].sName;
                int start = (offset + i) * 64;
                int end = start + 64;
                int sourceIndex = 0;
                for (int j = start; j < end; j++)
                {
                    existingState.mCarNames[j] = name[sourceIndex];
                    sourceIndex++;
                }
            }
            return existingState;
        }

        public static ams2APIStruct MergeWithExistingState(ams2APIStruct existingState, sVehicleClassNamesData vehicleClassNamesData)
        {            
            for (int i = 0; i < 60; i++)    // why 60 class names here? Who knows
            {
                // we only have 20 bytes of data per name here
                // understand this data and map it
            }
            return existingState;
        }

        public static String getCarClassName(ams2APIStruct shared, int participantIndex)
        {
            if (shared.mCarClassNames == null)
            {
                return "";
            }
            return getNameFromBytes(shared.mCarClassNames.Skip(participantIndex * 64).Take(64).ToArray());
        }

        public static String getNameFromBytes(byte[] bytes)
        {
            byte zero = (byte)0;
            // if the byte array is empty, or the first char is null, return empty string
            if (bytes == null || bytes.Length == 0 || (bytes.Length > 0 && bytes[0] == zero))
            {
                return "";
            }            
            int numBytesToDecode = Array.IndexOf(bytes, zero, 0);
            
            if (numBytesToDecode == -1)
            {
                // no nulls, we want the whole array
                numBytesToDecode = bytes.Length;
            }
            return ENCODING.GetString(bytes, 0, numBytesToDecode);
        }

        private static float[] toFloatArray(int[] intArray, float factor)
        {
            List<float> l = new List<float>();
            foreach (int i in intArray)
            {
                l.Add(((float)i) / factor);
            }
            return l.ToArray();
        }

        private static float[] toFloatArray(byte[] byteArray, float factor)
        {
            List<float> l = new List<float>();
            foreach (byte i in byteArray)
            {
                l.Add(((float)i) / factor);
            }
            return l.ToArray();
        }

        private static float[] toFloatArray(short[] shortArray, float factor)
        {
            List<float> l = new List<float>();
            foreach (short i in shortArray)
            {
                l.Add(((float)i) / factor);
            }
            return l.ToArray();
        }

        private static float[] toFloatArray(ushort[] ushortArray, float factor)
        {
            List<float> l = new List<float>();
            foreach (ushort i in ushortArray)
            {
                l.Add(((float)i) / factor);
            }
            return l.ToArray();
        }

        private static uint[] toUIntArray(byte[] byteArray)
        {
            List<uint> l = new List<uint>();
            foreach (byte i in byteArray)
            {
                l.Add((byte)i);
            }
            return l.ToArray();
        }
    }

    [Serializable]
    public struct CarClassNameString
    {
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 20)]
        public byte[] classNameByteArray;
    }

    [Serializable]
    public struct ams2APIParticipantStruct
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool mIsActive;

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mName;                                    // [ string ]

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mWorldPosition;                          // [ UNITS = World Space  X  Y  Z ]

        public float mCurrentLapDistance;                       // [ UNITS = Metres ]   [ RANGE = 0.0f->... ]    [ UNSET = 0.0f ]
        public uint mRacePosition;                              // [ RANGE = 1->... ]   [ UNSET = 0 ]
        public uint mLapsCompleted;                             // [ RANGE = 0->... ]   [ UNSET = 0 ]
        public uint mCurrentLap;                                // [ RANGE = 0->... ]   [ UNSET = 0 ]
        public int mCurrentSector;                             // [ enum (Type#4) Current Sector ]

        public override string ToString()
        {
            return "position " + mRacePosition + " name " + StructHelper.getNameFromBytes(mName) + " lapsCompleted " + mLapsCompleted + " lapDist " + mCurrentLapDistance;
        }
    }

    [Serializable]
    public struct ams2APIStruct
    {
        public override string ToString()
        {
            return "num participants " + mNumParticipants + " viewed index " + mViewedParticipantIndex + " driver name " +
                StructHelper.getNameFromBytes(mParticipantData[mViewedParticipantIndex].mName) + " LF tyre " + StructHelper.getNameFromBytes(mLFTyreCompoundName);
        }

         // Version Number
        public uint mVersion;                           // [ RANGE = 0->... ]
        public uint mBuildVersionNumber;                // [ RANGE = 0->... ]   [ UNSET = 0 ]

        // Game States
        public uint mGameState;                         // [ enum (Type#1) Game state ]
        public uint mSessionState;                      // [ enum (Type#2) Session state ]
        public uint mRaceState;                         // [ enum (Type#3) Race State ]

        // Participant Info
        public int mViewedParticipantIndex;                                  // [ RANGE = 0->STORED_PARTICIPANTS_MAX ]   [ UNSET = -1 ]
        public int mNumParticipants;                                         // [ RANGE = 0->STORED_PARTICIPANTS_MAX ]   [ UNSET = -1 ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public ams2APIParticipantStruct[] mParticipantData;    // [ struct (Type#13) ParticipantInfo struct ]

        // Unfiltered Input
        public float mUnfilteredThrottle;                        // [ RANGE = 0.0f->1.0f ]
        public float mUnfilteredBrake;                           // [ RANGE = 0.0f->1.0f ]
        public float mUnfilteredSteering;                        // [ RANGE = -1.0f->1.0f ]
        public float mUnfilteredClutch;                          // [ RANGE = 0.0f->1.0f ]

        // Vehicle information
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mCarName;                 // [ string ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mCarClassName;            // [ string ]

        // Event information
        public uint mLapsInEvent;                        // [ RANGE = 0->... ]   [ UNSET = 0 ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mTrackLocation;           // [ string ] - untranslated shortened English name
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mTrackVariation;          // [ string ]- untranslated shortened English variation description
        public float mTrackLength;                               // [ UNITS = Metres ]   [ RANGE = 0.0f->... ]    [ UNSET = 0.0f ]

        public int mmfOnly_mNumSectors;                                  // [ RANGE = 0->... ]   [ UNSET = -1 ]
        [MarshalAs(UnmanagedType.I1)]
        public bool mLapInvalidated;                             // [ UNITS = boolean ]   [ RANGE = false->true ]   [ UNSET = false ]
        public float mmfOnly_mBestLapTime;                               // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mLastLapTime;                               // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mmfOnly_mCurrentTime;                               // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mSplitTimeAhead;                            // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mSplitTimeBehind;                           // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mSplitTime;                                 // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mEventTimeRemaining;                        // [ UNITS = milli-seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mPersonalFastestLapTime;                    // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mWorldFastestLapTime;                       // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mmfOnly_mCurrentSector1Time;                        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mmfOnly_mCurrentSector2Time;                        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mmfOnly_mCurrentSector3Time;                        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mmfOnly_mFastestSector1Time;                        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mmfOnly_mFastestSector2Time;                        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mmfOnly_mFastestSector3Time;                        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mPersonalFastestSector1Time;                // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mPersonalFastestSector2Time;                // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mPersonalFastestSector3Time;                // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mWorldFastestSector1Time;                   // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mWorldFastestSector2Time;                   // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        public float mWorldFastestSector3Time;                   // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]

        // Flags
        public uint mHighestFlagColour;                 // [ enum (Type#5) Flag Colour ]
        public uint mHighestFlagReason;                 // [ enum (Type#6) Flag Reason ]

        // Pit Info
        public uint mPitMode;                           // [ enum (Type#7) Pit Mode ]
        public uint mPitSchedule;                       // [ enum (Type#8) Pit Stop Schedule ]

        // Car State
        public uint mCarFlags;                          // [ enum (Type#9) Car Flags ]
        public float mOilTempCelsius;                           // [ UNITS = Celsius ]   [ UNSET = 0.0f ]
        public float mOilPressureKPa;                           // [ UNITS = Kilopascal ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mWaterTempCelsius;                         // [ UNITS = Celsius ]   [ UNSET = 0.0f ]
        public float mWaterPressureKPa;                         // [ UNITS = Kilopascal ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mFuelPressureKPa;                          // [ UNITS = Kilopascal ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mFuelLevel;                                // [ RANGE = 0.0f->1.0f ]
        public float mFuelCapacity;                             // [ UNITS = Liters ]   [ RANGE = 0.0f->1.0f ]   [ UNSET = 0.0f ]
        public float mSpeed;                                    // [ UNITS = Metres per-second ]   [ RANGE = 0.0f->... ]
        public float mRpm;                                      // [ UNITS = Revolutions per minute ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mMaxRPM;                                   // [ UNITS = Revolutions per minute ]   [ RANGE = 0.0f->... ]   [ UNSET = 0.0f ]
        public float mBrake;                                    // [ RANGE = 0.0f->1.0f ]
        public float mThrottle;                                 // [ RANGE = 0.0f->1.0f ]
        public float mClutch;                                   // [ RANGE = 0.0f->1.0f ]
        public float mSteering;                                 // [ RANGE = -1.0f->1.0f ]
        public int mGear;                                       // [ RANGE = -1 (Reverse)  0 (Neutral)  1 (Gear 1)  2 (Gear 2)  etc... ]   [ UNSET = 0 (Neutral) ]
        public int mNumGears;                                   // [ RANGE = 0->... ]   [ UNSET = -1 ]
        public float mOdometerKM;                               // [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.I1)]
        public bool mAntiLockActive;                            // [ UNITS = boolean ]   [ RANGE = false->true ]   [ UNSET = false ]
        public int mLastOpponentCollisionIndex;                 // [ RANGE = 0->STORED_PARTICIPANTS_MAX ]   [ UNSET = -1 ]
        public float mLastOpponentCollisionMagnitude;           // [ RANGE = 0.0f->... ]
        [MarshalAs(UnmanagedType.I1)]
        public bool mBoostActive;                               // [ UNITS = boolean ]   [ RANGE = false->true ]   [ UNSET = false ]
        public float mBoostAmount;                              // [ RANGE = 0.0f->100.0f ] 

  // Motion & Device Related
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mOrientation;                     // [ UNITS = Euler Angles. The rotation order isn't known here. 
                                                         // From the data it appears it's actually pitch-yaw-roll or close enough to use like that ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mLocalVelocity;                   // [ UNITS = Metres per-second ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mWorldVelocity;                   // [ UNITS = Metres per-second ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mAngularVelocity;                 // [ UNITS = Radians per-second ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mLocalAcceleration;               // [ UNITS = Metres per-second ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mWorldAcceleration;               // [ UNITS = Metres per-second ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] mExtentsCentre;                   // [ UNITS = Local Space  X  Y  Z ]

  // Wheels / Tyres
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] mTyreFlags;               // [ enum (Type#10) Tyre Flags ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] mTerrain;                 // [ enum (Type#11) Terrain Materials ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreY;                          // [ UNITS = Local Space  Y ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreRPS;                        // [ UNITS = Revolutions per second ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreSlipSpeed;                  // OBSOLETE, kept for backward compatibility only
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreTemp;                       // [ UNITS = Celsius ]   [ UNSET = 0.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreGrip;                       // OBSOLETE, kept for backward compatibility only
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreHeightAboveGround;          // [ UNITS = Local Space  Y ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreLateralStiffness;           // OBSOLETE, kept for backward compatibility only
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreWear;                       // [ RANGE = 0.0f->1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mBrakeDamage;                    // [ RANGE = 0.0f->1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mSuspensionDamage;               // [ RANGE = 0.0f->1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mBrakeTempCelsius;               // [ UNITS = Celsius ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreTreadTemp;                  // [ UNITS = Kelvin ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreLayerTemp;                  // [ UNITS = Kelvin ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreCarcassTemp;                // [ UNITS = Kelvin ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreRimTemp;                    // [ UNITS = Kelvin ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreInternalAirTemp;            // [ UNITS = Kelvin ]

  // Car Damage
        public uint mCrashState;                        // [ enum (Type#12) Crash Damage State ]
        public float mAeroDamage;                               // [ RANGE = 0.0f->1.0f ]
        public float mEngineDamage;                             // [ RANGE = 0.0f->1.0f ]

  // Weather
        public float mAmbientTemperature;                       // [ UNITS = Celsius ]   [ UNSET = 25.0f ]
        public float mTrackTemperature;                         // [ UNITS = Celsius ]   [ UNSET = 30.0f ]
        public float mRainDensity;                              // [ UNITS = How much rain will fall ]   [ RANGE = 0.0f->1.0f ]
        public float mWindSpeed;                                // [ RANGE = 0.0f->100.0f ]   [ UNSET = 2.0f ]
        public float mWindDirectionX;                           // [ UNITS = Normalised Vector X ]
        public float mWindDirectionY;                           // [ UNITS = Normalised Vector Y ]
        // from sunny all the way to light rain = 2 (at start of session - not transition), rain to thunder storm, blizzard, fog = 1.5. 
        // Transitions from 2 down to 1.9ish as rain starts.
        // NOTE: this is not in the UDP data for ams2 (why?)
        public float mCloudBrightness;                          // [ RANGE = 0.0f->... ]

  //AMS2 additions start, version 8
	// Sequence Number to help slightly with data integrity reads
        public uint mSequenceNumber;          // 0 at the start, incremented at start and end of writing, so odd when Shared Memory is being filled, even when the memory is not being touched

	//Additional car variables
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mWheelLocalPositionY;           // [ UNITS = Local Space  Y ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mSuspensionTravel;              // [ UNITS = meters ] [ RANGE 0.f =>... ]  [ UNSET =  0.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mSuspensionVelocity;            // [ UNITS = Rate of change of pushrod deflection ] [ RANGE 0.f =>... ]  [ UNSET =  0.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mAirPressure;                   // [ UNITS = kPa ]  [ RANGE 0.f =>... ]  [ UNSET =  0.0f ]
        public float mEngineSpeed;                             // [ UNITS = Rad/s ] [UNSET = 0.f ]
        public float mEngineTorque;                            // [ UNITS = Newton Meters] [UNSET = 0.f ] [ RANGE = 0.0f->... ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public float[] mWings;                                // [ RANGE = 0.0f->1.0f ] [UNSET = 0.f ]
        public float mHandBrake;                               // [ RANGE = 0.0f->1.0f ] [UNSET = 0.f ]

	    // additional race variables for each participant
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mCurrentSector1Times;        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mCurrentSector2Times;        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mCurrentSector3Times;        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mFastestSector1Times;        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mFastestSector2Times;        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mFastestSector3Times;        // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mFastestLapTimes;            // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public float[] mLastLapTimes;               // [ UNITS = seconds ]   [ RANGE = 0.0f->... ]   [ UNSET = -1.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mLapsInvalidated;            // [ UNITS = boolean for all participants ]   [ RANGE = false->true ]   [ UNSET = false ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public uint[] mRaceStates;         // [ enum (Type#3) Race State ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public uint[] mPitModes;           // [ enum (Type#7)  Pit Mode ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64*3)]
        public float[] mOrientations;      // [ UNITS = Euler Angles ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
	    public float[] mSpeeds;                     // [ UNITS = Metres per-second ]   [ RANGE = 0.0f->... ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64*64)]
        public byte[] mCarNames; // [ string ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64*64)]
        public byte[] mCarClassNames; // [ string ]
        /*[MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public ams2APIParticipantAdditionalDataStruct[] mAdditionalParticipantData;   */   // 
																											// additional race variables
        public int mEnforcedPitStopLap;                          // [ UNITS = in which lap there will be a mandatory pitstop] [ RANGE = 0.0f->... ] [ UNSET = -1 ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mTranslatedTrackLocation;  // [ string ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] mTranslatedTrackVariation; // [ string ]]

        public float mBrakeBias;																		// [ RANGE = 0.0f->1.0f... ]   [ UNSET = -1.0f ]
	    public float mTurboBoostPressure;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] mLFTyreCompoundName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] mRFTyreCompoundName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] mLRTyreCompoundName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] mRRTyreCompoundName;
	    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public uint[] mPitSchedules;  // [ enum (Type#7)  Pit Mode ]
	    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public uint[] mHighestFlagColours;                 // [ enum (Type#5) Flag Colour ]
	    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public uint[] mHighestFlagReasons;                 // [ enum (Type#6) Flag Reason ]
	    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public uint[] mNationalities;					   // [ nationality table , SP AND UNSET = 0 ] See nationalities.txt file for details
	    public float mSnowDensity;                         // [ UNITS = How much snow will fall ]   [ RANGE = 0.0f->1.0f ], this will be non zero only in Snow season, in other seasons whatever is falling from the sky is reported as rain


        // AMS2 Additions (v10...)

        // Session info
        public float mSessionDuration;           // [ UNITS = seconds ]   [ UNSET = 0.0f ]  The scheduled session Length (unset means laps race. See mLapsInEvent)
        // note the API doc says this is minutes, but it's actually seconds
        public int mSessionAdditionalLaps;     // The number of additional complete laps lead lap drivers must complete to finish a timed race after the session duration has elapsed.

        // Tyres
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreTempLeft;    // [ UNITS = Celsius ]   [ UNSET = 0.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreTempCenter;  // [ UNITS = Celsius ]   [ UNSET = 0.0f ]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mTyreTempRight;   // [ UNITS = Celsius ]   [ UNSET = 0.0f ]

        // DRS
        public uint mDrsState;           // [ enum (Type#14) DrsState ]

        // Suspension
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] mRideHeight;      // [ UNITS = cm ]

        // Input
        public byte mJoyPad0;            // button mask
        public byte mDPad;               // button mask

        // added for version 13:

        public int mAntiLockSetting;             // [ UNSET = -1 ] Current ABS garage setting. Valid under player control only.
        public int mTractionControlSetting;      // [ UNSET = -1 ] Current ABS garage setting. Valid under player control only.

        // ERS
        public int mErsDeploymentMode;           // [ enum (Type#15)  ErsDeploymentMode ]
        public bool mErsAutoModeEnabled;         // true if the deployment mode was selected by auto system. Valid only when mErsDeploymentMode > ERS_DEPLOYMENT_MODE_NONE

        // Clutch State & Damage
        // note temp and wear appear to be the wrong way around in the API
        public float mClutchWear;                // [ RANGE = 0.0f->1.0f... ] - this is always 0
        public float mClutchTemp;                // [ UNITS = Kelvin ] [ UNSET = -273.16 ] - this appears to have no bearing on the overheated value below
        public bool mClutchOverheated;          // true if clutch performance is degraded due to overheating
        public bool mClutchSlipping;            // true if clutch is slipping (can be induced by overheating or wear)

        public int mYellowFlagState;             // [ enum (Type#16) YellowFlagState ]
    }
}