using System;
using System.Collections.Generic;
using System.Linq;
using InSimDotNet.Packets;

namespace CrewChiefV4.LFS
{
    /// <summary>
    /// In-memory data model storing the current Live for Speed simulation state,
    /// tracking active players, connections, session state, lap timing, flags, and incidents.
    /// </summary>
    class LFSData
    {
        public int InSimVersion;

        public SessionState SessionState = new SessionState();

        public Dictionary<byte, Connection> Connections = new Dictionary<byte, Connection>();
        public Dictionary<byte, Player> Players = new Dictionary<byte, Player>();

        public Player GetPlayerByPLID(byte plid)
        {
            return Players.ContainsKey(plid) ? Players[plid] : null;
        }

        public Connection GetConnectionByUCID(byte ucid)
        {
            return Connections.ContainsKey(ucid) ? Connections[ucid] : null;
        }

        public WavedFlag WavedFlag;
        public int CutTrackWarnings;
        public DateTime LastIncidentTime = DateTime.MinValue;
        public byte LastIncidentPLID;
        public int LastIncidentSpClose;

        // OutSim telemetry
        public bool HasOutSimData;
        public DateTime LastOutSimTime = DateTime.MinValue;
        public byte[] TyreAirTemps = new byte[4]; // 0: RL, 1: RR, 2: FL, 3: FR
        public float OutSimCurrentLapDist;

        /// <summary>
        ///  Create a deep copy of this game state. Used for saving debug traces.
        /// </summary>
        public LFSData Copy()
        {
            return new LFSData
            {
                InSimVersion = InSimVersion,
                SessionState = SessionState.Copy(),
                Connections = CopyConnections(),
                Players = CopyPlayers(),
                WavedFlag = WavedFlag,
                CutTrackWarnings = CutTrackWarnings,
                LastIncidentTime = LastIncidentTime,
                LastIncidentPLID = LastIncidentPLID,
                LastIncidentSpClose = LastIncidentSpClose,
                HasOutSimData = HasOutSimData,
                LastOutSimTime = LastOutSimTime,
                TyreAirTemps = TyreAirTemps != null ? (byte[])TyreAirTemps.Clone() : new byte[4],
                OutSimCurrentLapDist = OutSimCurrentLapDist,
            };
        }

        public Dictionary<byte, Connection> CopyConnections()
        {
            var copy = new Dictionary<byte, Connection>();
            foreach (var entry in Connections)
            {
                copy[entry.Key] = entry.Value.Copy();
            }

            return copy;
        }

        public Dictionary<byte, Player> CopyPlayers()
        {
            var copy = new Dictionary<byte, Player>();
            foreach (var entry in Players)
            {
                copy[entry.Key] = entry.Value.Copy();
            }

            return copy;
        }
    }

    class SessionState
    {
        /// <summary>
        /// Session start time
        /// </summary>
        public DateTime StartTime;

        /// <summary>
        /// Time when the lights went green (known after first split time)
        /// </summary>
        public DateTime GreenTime;

        public StateFlags StateFlags;

        /// <summary>
        /// Must pit, can reset, etc.
        /// </summary>
        public RaceFlags SessionFlags;

        /// <summary>
        /// Unique ID of viewed player (0 = none)
        /// </summary>
        public byte ViewPLID;

        /// <summary>
        /// 0 = no race / 1 = race / 2 = qualifying
        /// </summary>
        public LFSSessionType RaceInProg;

        /// <summary>
        /// qualifying time in minutes
        /// </summary>
        public byte QualMins;

        /// <summary>
        /// 0       : practice
        /// 1-99    : number of laps...   laps  = rl
        /// 100-190 : 100 to 1000 laps... laps  = (rl - 100) * 10 + 100
        /// 191-238 : 1 to 48 hours...    hours = rl - 190
        /// </summary>
        public byte RaceLaps;

        /// <summary>
        /// 0 ,1, 2... depending on track
        /// </summary>
        public byte Weather;

        /// <summary>
        /// short name for track e.g.FE2R
        /// </summary>
        public string Track;

        /// <summary>
        /// 0 = off / 1 = weak / 2 = strong
        /// </summary>
        public WindStrength Wind;
        public bool LeaderFinished;
        public bool PlayerFinished;

        public int NumNodes;
        public int Split1;
        public int Split2;
        public int Split3;

        public int NumberOfSectors
        {
            get
            {
                if (Split3 > 0) return 4;
                if (Split2 > 0) return 3;
                if (Split1 > 0) return 2;
                return 1;
            }
        }

        public SessionState Copy()
        {
            return new SessionState
            {
                StartTime = StartTime,
                GreenTime = GreenTime,
                StateFlags = StateFlags,
                SessionFlags = SessionFlags,
                ViewPLID = ViewPLID,
                RaceInProg = RaceInProg,
                QualMins = QualMins,
                RaceLaps = RaceLaps,
                Weather = Weather,
                Track = Track,
                Wind = Wind,
                LeaderFinished = LeaderFinished,
                PlayerFinished = PlayerFinished,
                NumNodes = NumNodes,
                Split1 = Split1,
                Split2 = Split2,
                Split3 = Split3,
            };
        }
    }

    class Connection
    {
        public byte UCID;
        public string UName;
        public string PName;

        public Connection Copy()
        {
            return new Connection
            {
                UCID = UCID,
                UName = UName,
                PName = PName,
            };
        }
    }

    class Player
    {
        public byte PLID;

        public Connection Connection;

        public PlayerScoring Scoring;

        public PitData PitData;

        /// <summary>
        /// Bit 0: female / bit 1: AI / bit 2: remote
        /// </summary>
        public PlayerTypes PType;

        /// <summary>
        /// Nickname
        /// </summary>
        public string PName;

        /// <summary>
        /// Car name
        /// </summary>
        public string CName;

        /// <summary>
        /// Initial fuel from setup / grid (0..100%, 255 = not available)
        /// </summary>
        public byte InitialFuel = 255;

        /// <summary>
        /// Fuel remaining (0..200 = 0..100%, 255 = not available)
        /// </summary>
        public byte Fuel200 = 255;

        /// <summary>
        /// Active penalty
        /// </summary>
        public PenaltyValue Penalty = PenaltyValue.PENALTY_NONE;

        public void ResetOnSessionStart()
        {
            if (Scoring != null)
            {
                Scoring.Reset();
            }
            Fuel200 = InitialFuel != 255 ? (byte)(InitialFuel * 2) : (byte)255;
            Penalty = PenaltyValue.PENALTY_NONE;
            if (PitData != null)
            {
                PitData.NumStops = 0;
                PitData.PitLaneFact = PitLaneFact.PITLANE_EXIT;
            }
        }

        public Player Copy()
        {
            return new Player
            {
                PLID = PLID,
                Connection = Connection != null ? Connection.Copy() : null,
                Scoring = Scoring != null ? Scoring.Copy() : null,
                PitData = PitData != null ? PitData.Copy() : null,
                PType = PType,
                PName = PName,
                CName = CName,
                InitialFuel = InitialFuel,
                Fuel200 = Fuel200,
                Penalty = Penalty,
            };
        }
    }

    class PlayerScoring
    {
        /// <summary>
        /// Split number 1, 2, 3
        /// </summary>
        public byte Split;

        public Split Split1 = new Split();
        public Split Split2 = new Split();
        public Split Split3 = new Split();

        public List<Lap> LapData = new List<Lap>();
        public int LapsDone;

        public void Reset()
        {
            Split = 0;
            Split1 = new Split();
            Split2 = new Split();
            Split3 = new Split();
            LapData.Clear();
            LapsDone = 0;
            Node = 0;
            Speed = 0;
        }

        public Lap GetLastLap()
        {
            return LapData.Count == 0 ? null : LapData.Last();
        }

        public Split GetCurrentSplit()
        {
            switch (Split)
            {
                case 1:
                    return Split1;
                case 2:
                    return Split2;
                case 3:
                    return Split3;
                default:
                    return Split1;
            }
        }

        /// <summary>
        /// Current path node on track
        /// </summary>
        public int Node;

        /// <summary>
        /// Current race position: 0 = unknown, 1 = leader, etc.
        /// </summary>
        public byte Position;

        /// <summary>
        /// Flags, lag etc.
        /// </summary>
        public CompCarFlags Info;

        /// <summary>
        /// X map (65536 = 1 meter)
        /// </summary>
        public int X;

        /// <summary>
        /// Y map (65536 = 1 meter)
        /// </summary>
        /// 
        public int Y;

        /// <summary>
        /// Z alt (65536 = 1 meter)
        /// </summary>
        public int Z;

        /// <summary>
        /// Speed (32768 = 100 m/s)
        /// </summary>
        public int Speed;

        /// <summary>
        /// Car's motion if Speed > 0: 0 = world y direction, 32768 = 180 deg
        /// </summary>
        public int Direction;

        /// <summary>
        /// Direction of forward axis: 0 = world y direction, 32768 = 180 deg
        /// </summary>
        public int Heading;

        /// <summary>
        /// Signed, rate of change of heading: (16384 = 360 deg/s)
        /// </summary>
        public int AngVel;

        public PlayerScoring Copy()
        {
            return new PlayerScoring
            {
                Split = Split,
                Split1 = Split1,
                Split2 = Split2,
                Split3 = Split3,
                LapData = LapData != null ? new List<Lap>(LapData) : new List<Lap>(),
                LapsDone = LapsDone,
                Position = Position,
                Info = Info,
                Node = Node,
                X = X,
                Y = Y,
                Z = Z,
                Speed = Speed,
                Direction = Direction,
                Heading = Heading,
                AngVel = AngVel,
            };
        }
    }

    class PitData
    {
        public PitLaneFact PitLaneFact;
        public byte NumStops;

        public PitData Copy()
        {
            return new PitData
            {
                PitLaneFact = PitLaneFact,
                NumStops = NumStops,
            };
        }
    }

    struct Split
    {
        /// <summary>
        /// Split time
        /// </summary>
        public TimeSpan STime;


        /// <summary>
        /// Time since race start
        /// </summary>
        public TimeSpan ETime;

        public Split Copy()
        {
            return new Split
            {
                STime = STime,
                ETime = ETime,
            };
        }
    }

    class Lap
    {
        public TimeSpan LTime;
        public TimeSpan ETime;

        /// <summary>
        /// Split number 1, 2, 3
        /// </summary>
        public byte Split;

        public Split Split1;
        public Split Split2;
        public Split Split3;

        public Lap Copy()
        {
            return new Lap
            {
                LTime = LTime,
                ETime = ETime,
                Split1 = Split1,
                Split2 = Split2,
                Split3 = Split3,
            };
        }
    }

    struct WavedFlag
    {
        public byte PLID;
        public bool IsOn;
        public FlagType Flag;
        public byte CarBehind;

        public WavedFlag Copy()
        {
            return new WavedFlag
            {
                PLID = PLID,
                IsOn = IsOn,
                Flag = Flag,
                CarBehind = CarBehind,
            };
        }
    }
}