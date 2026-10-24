/*
RBR reverse engineered structures.  Allows access to native C++ structs from C#.
Must be kept in sync with the RBR Crew Chief plugin's RBRData.h.

Author: The Iron Wolf (vleonavicius@hotmail.com)
Website: thecrewchief.org
*/
using Newtonsoft.Json;
using System;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

// CC specific: Mark more common unused members with JsonIgnore for reduced trace sizes.

namespace CrewChiefV4.RBR
{
    // Marshalled types:
    // C++                 C#
    // char          ->    byte
    // unsigned char ->    byte
    // signed char   ->    sbyte
    // bool          ->    byte
    // long          ->    int
    // unsigned long ->    uint
    // short         ->    short
    // unsigned short ->   ushort
    // ULONGLONG     ->    Int64
    public class Constants
    {
        public const string MM_MAPPED_PER_FRAME_DATA_FILE_NAME = "$RBRCC_PerFrameData$";
        public const string MM_MAPPED_CODRIVER_DATA_FILE_NAME = "$RBRCC_CoDriverData$";
        public const string MM_MAPPED_EXTENDED_FILE_NAME = "$RBRCC_Extended$";

        public const int MAX_PACENOTES_SUPPORTED = 4096;

        public enum GameMode
        {
            NotAvailable = 0,
            Driving = 1,
            Paused = 2,
            InMenu = 3,
            Unknown1 = 4,
            LoadingTrack = 5,
            ExitingRaceOrReplay = 6,  //06 = exiting to menu from a race or replay (after this the mode goes to 12 for a few secs and finally to 3 when the game is showing the main or plugin menu)
            ExitingGame = 7,
            Replay = 8,
            SessionEnd = 9,
            BeforeReplay = 10,
            Unknown2 = 11,
            LoadingGame = 12,
            Crashing1 = 13,
            Crashing2 = 14,
            Unknow3 = 15,
            SessionStart = 16,
            Unknown4 = 17,
            ExitingRaceOrReplay2 = 18,
            SwitchingOutOfMenu = 19
        }
    }

    namespace RBRData
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRGameMode
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x728)]
            [JsonIgnore] public byte[] pad1;

            // gameMode
            //		00 = (not available)
            //		01 = driving (after 5secs or less left in start clock or already driving after GO! command)
            //		02 = pause (when a menu is shown while stage or replay is running)
            //		03 = main menu or plugin menu (stage not running)
            //		04 = ? (black out)
            //		05 = loading track (race or replay)
            //		06 = exiting to menu from a race or replay (after this the mode goes to 12 for a few secs and finally to 3 when the game is showing the main or plugin menu)
            //		07 = quit the application ?
            //		08 = replay
            //		09 = end lesson / finish race / retiring / end replay
            //      0A = Before starting a replay (camera is spinning around the car)
            //      0B = ? (black out)
            //      0C = Game is starting (loading the initial "Load Profile" screen)
            //      0D = (not available) (status goes to 0x0A, camera spins and then RBR crashes)
            //      0E = (not available) (status goes to 0x0F and then RBR crashes)
            //      0F = ? Doesnt work anymore. Goes to menu? Pause racing and replaying and hide all on-screen instruments and timers (supported only after the race or replay has started, ie 0x0A and 0x10 status has changed to 0x08 or 0x01)
            //		10 = Before starting a race start countdown (the camera is moving around the car at starting line or in replay)
            //		11 = ? (black out)
            //		12 = go to the main or plugin menu (stage finished or retired at this point)
            //		13 = switch screen from menu to something else
            //		14-255 = ?
            public int gameMode; // 0x728
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRGameModeExt
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x10)]
            [JsonIgnore] public byte[] pad1;
            public int gameModeExt;        // 0x10. 0x00 = Racing active. Update car model movement (or if set during replay then freeze the car movement)
                                           //       0x01 = Loading replay. If racing active then don't react to controllers, but the car keeps on moving. During replay stops the car position updates.
                                           //       0x02 = Replay mode (update car movements).
                                           //       0x03 = Plugin menu open (if replaying then stop the car updates)
                                           //       0x04 = Pause replay (Pacenote plugin uses this value to pause replay)
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x18 - 0x10 - sizeof(int))]
            [JsonIgnore] public byte[] pad2;
            public int carID;              // 0x18 00..07 = The current racing or replay car model slot#
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRCarInfo
        {
            public int hudPositionX;  // 0x00
            public int hudPositionY;  // 0x04
            public int raceStarted;   // 0x08 (1=Race started. Start countdown less than 5 secs, so false start possible, 0=Race not yet started or start countdown still more than 5 secs and gas pedal doesn't work yet)
            public float speed;             // 0x0C
            public float rpm;               // 0x10
            public float temp;              // 0x14 (water temp in celsius?)
            public float turbo;             // 0x18. (pressure, in Pascals?)
            public int unknown2;        // 0x1C
            public float distanceFromStartControl; // 0x20
            public float distanceTravelled; // 0x24
            public float distanceToFinish;  // 0x28   >0 Meters left to finish line, <0 Crossed the finish line (meters after finish line)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x13C - 0x28 - sizeof(float))]
            [JsonIgnore] public byte[] pad1;

            public float stageProgress;     // 0x13C  (meters, hundred meters, some map unit?. See RBRMapInfo.stageLength also)
            public float raceTime;          // 0x140
            public int raceFinished;    // 0x144  (0=Racing after GO! command, 1=Race not yet started (GO! not yet shouted), race stopped or retired or completed)
            public int unknown4;        // 0x148
            public int unknown5;        // 0x14C
            public int drivingDirection;// 0x150. 0=Correct direction, 1=Car driving to wrong direction
            public float fadeWrongWayMsg;   // 0x154. 1 when "wrong way" msg is shown
                                            //	TODO: 0x15C Some time attribute? Total race time? 
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x170 - 0x154 - sizeof(float))]
            [JsonIgnore] public byte[] pad3;

            public int gear;            // 0x170. 0=Reverse,1=Neutral,2..6=Gear-1 (ie. value 3 means gear 2)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x244 - 0x170 - sizeof(int))]
            [JsonIgnore] public byte[] pad4;

            public float stageStartCountdown; // 0x244 (7=Countdown not yet started, 6.999-0.1 Countdown running, 0=GO!, <0=Racing time since GO! command)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x254 - 0x244 - sizeof(float))]
            [JsonIgnore] public byte[] pad5;

            public int splitReachedNo;  // 0x254 0=Start line passed if race is on, 1=Split#1 passed, 2=Split#2 passed
            public float split1Time;        // 0x258
            public float split2Time;        // 0x25C
            public float unknown6;          // 0x260

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x2C4 - 0x260 - sizeof(float))]
            [JsonIgnore] public byte[] pad6;

            public float unknown7;          // 0x2C4	TODO: stageFinished?  0=Stage not started or running, 1=Stage finished (int or float?) (?)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x758 - 0x2C4 - sizeof(float))]
            [JsonIgnore] public byte[] pad7;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
            [JsonIgnore] public byte[] pCamera;     // 0x758  Pointer to camera data

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0xEF8 - 0x758 - 0x4)]
            [JsonIgnore] public byte[] pad8;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            public float[] carPosition;    // 0xEF8..F00 (3 floats, car position X,Y,Z
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRCarControls
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x738 + 0x5C)]
            [JsonIgnore] public byte[] pad1;

            public float steering;     // 0x5C (0.0 - 1.0 float value representing left 0%-49.9%, center 50.0%, right 50.1%-100%)
            public float throttle;     // 0x60 (0.0 - 1.0 float value)
            public float brake;        // 0x64 (0.0 - 1.0 float value)
            public float handBrake;    // 0x68 (0.0 or 1.0)
            public float clutch;       // 0x6C (0.0 or 1.0 float value >0.85 clutch on, <=0.85 clutch off)
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRMapSettings
        {
            public int unknown1;   // 0x00
            public int trackID;    // 0x04   (xx trackID)
            public int carID;      // 0x08   (0..7 carID)
            public int unknown2;   // 0x0C
            public int unknown3;   // 0x10
            public int transmissionType; // 0x14  (0=Manual, 1=Automatic)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x30 - 0x14 - sizeof(int))]
            [JsonIgnore] public byte[] pad1; // Padding to reach 0x30

            public int racePaused; // 0x30 (0=Normal mode, 1=Racing in paused state)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x38 - 0x30 - sizeof(int))]
            [JsonIgnore] public byte[] pad2; // Padding to reach 0x38

            public int tyreType;   // 0x38 (0=Dry tarmac, 1=Intermediate tarmac, 2=Wet tarmac, 3=Dry gravel, 4=Inter gravel, 5=Wet gravel, 6=Snow)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x48 - 0x38 - sizeof(int))]
            [JsonIgnore] public byte[] pad3; // Padding to reach 0x48

            public int weatherType;    // 0x48   (0=Good, 1=Random, 3=Bad)
            public int unknown4;       // 0x4C
            public int damageType;     // 0x50   (0=No damange, 1=Safe, 2=Reduced, 3=Realistic)
            public int pacecarEnabled; // 0x54   (0=Pacecar disabled, 1=Pacecar enabled)
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RRBRCarMovement
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x100)]
            [JsonIgnore] public byte[] pad1;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x4)]
            public float[] carQuat;         // 0x100..0x10C (4 floats). Car look direction x,y,z,w

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x10)]
            public float[] carMapLocation;   // 0x110..0x14C (4x4 matrix of floats). _41.._44 is the current map position of the car

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x190 - 0x110 - sizeof(float) * 0x10)]
            [JsonIgnore] public byte[] pad2;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x3)]
            public float[] spin;               // 0x190  (Spin X,Y,Z)

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x1C0 - 0x190 - sizeof(float) * 0x3)]
            [JsonIgnore] public byte[] pad3;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x3)]
            public float[] speed;				// 0x1C0 (Speed/Velocity? X,Y,Z)
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRPacenote
        {
            public int type;       // 0x00
            public int flags;  // 0x04
            public float distance; // 0x08
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct RBRPacenotes
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 0x20)]
            [JsonIgnore] public byte[] pad1;

            public int numPacenotes;           // 0x20
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
            [JsonIgnore] public byte[] pPacenotes; // 0x24
        }

        ///////////////////////////////////////////////////////////////
        // Wrapper structures.

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct RBRMappedBufferVersionBlock
        {
            // If both version variables are equal, buffer is not being written to, or we're extremely unlucky and second check is necessary.
            // If versions don't match, buffer is being written to, or is incomplete (game crash, or missed transition).
            [JsonIgnore] public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
            [JsonIgnore] public uint mVersionUpdateEnd;            // Incremented after buffer write is done.
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct RBRMappedBufferVersionBlockWithSize
        {
            [JsonIgnore] public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
            [JsonIgnore] public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

            [JsonIgnore] public int mBytesUpdatedHint;             // How many bytes of the structure were written during the last update.
                                                                   // 0 means unknown (whole buffer should be considered as updated).
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct RBRPerFrameData
        {
            [JsonIgnore] public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
            [JsonIgnore] public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

            public RBRGameMode mRBRGameMode;
            public RBRGameModeExt mRBRGameModeExt;
            public RBRCarInfo mRBRCarInfo;
            public RBRCarControls mRBRCarControls;
            public RBRMapSettings mRBRMapSettings;
            public RRBRCarMovement mRBRCarMovement;
            public int stageLength;   // 0x75310  Length of current stage (to the time checking station few meters after the finish line. RBRCarInfo.stageProgress value is related to this total length) ?

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2 * 1024)]
            public byte[] currentLocationStringWide;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct RBRCoDriverData
        {
            [JsonIgnore] public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
            [JsonIgnore] public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

            public RBRPacenotes mRBRPacenoteInfo;

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = Constants.MAX_PACENOTES_SUPPORTED)]
            public RBRPacenote[] mPacenotes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct RBRExtended
        {
            [JsonIgnore] public uint mVersionUpdateBegin;          // Incremented right before buffer is written to.
            [JsonIgnore] public uint mVersionUpdateEnd;            // Incremented after buffer write is done.

            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] mVersion;                            // API version
        }
    }
}