using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InSimDotNet;
using InSimDotNet.Helpers;
using InSimDotNet.Packets;

namespace CrewChiefV4.LFS
{
    /// <summary>
    /// Reads game telemetry and state from Live for Speed using the InSim protocol via InSimDotNet.
    /// Manages TCP/UDP connection, packet subscription, session synchronization, race events,
    /// and spawns <see cref="LFSOutSimReader"/> for high-frequency UDP tyre telemetry.
    /// </summary>
    class LFSInSimReader : GameDataReader
    {
        private List<LFSStructWrapper> dataToDump;
        private LFSStructWrapper[] dataReadFromFile = null;
        private LFSStructWrapper workingData = new LFSStructWrapper();
        private readonly LFSStructWrapper lfsData = new LFSStructWrapper();
        private int dataReadFromFileIndex = 0;
        private String lastReadFileName = null;
        private InSim insim;
        private LFSOutSimReader outSimReader;
        private bool initialised;

        public static LFSInSimReader Instance { get; private set; }

        /// <summary>
        /// Retrieves a localized message from ui_text, falling back to English default if not found.
        /// </summary>
        public static string GetLocalizedMessage(string key, string fallbackEnglish, params object[] args)
        {
            string template = Configuration.getUIString(key);
            if (string.IsNullOrEmpty(template) || template == key)
            {
                template = fallbackEnglish;
            }
            return args != null && args.Length > 0 ? string.Format(template, args) : template;
        }

        /// <summary>
        /// Sends an in-game local message to the LFS player's chat using IS_MSL packet.
        /// </summary>
        public static void SendMessageLocal(string message, MessageSound sound = MessageSound.SND_SILENT)
        {
            if (Instance != null && Instance.insim != null && Instance.insim.IsConnected)
            {
                try
                {
                    if (string.IsNullOrEmpty(message))
                        return;

                    if (message.Length > 127)
                    {
                        message = message.Substring(0, 127);
                    }

                    Instance.insim.Send(new IS_MSL
                    {
                        Msg = message,
                        Sound = sound
                    });
                }
                catch (Exception ex)
                {
                    Log.Debug($"Failed to send IS_MSL: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Formats and sends an in-game fuel status message via IS_MSL.
        /// Since LFS manages fuel in percentage, this provides the player with accurate % readout
        /// matching the F12 in-game pit menu.
        /// </summary>
        public static void SendFuelStatus(float currentFuelPct, float consumptionPct, int lapsRemaining, float refuelPct = -1)
        {
            if (currentFuelPct < 0)
                return;

            string msg;
            if (refuelPct > 0)
            {
                msg = GetLocalizedMessage("lfs_msg_refuel", "^3[CrewChief] F12 Refuel: +{0}% (Current: {1:0.0}%)", (int)Math.Ceiling(refuelPct), currentFuelPct);
            }
            else if (lapsRemaining <= 2 && lapsRemaining > 0)
            {
                msg = GetLocalizedMessage("lfs_msg_fuel_warning", "^1[CrewChief] FUEL WARNING: {0:0.0}% remaining (~{1} lap{2})", currentFuelPct, lapsRemaining, lapsRemaining > 1 ? "s" : "");
            }
            else if (consumptionPct > 0)
            {
                msg = GetLocalizedMessage("lfs_msg_fuel_status", "^3[CrewChief] Fuel: {0:0.0}% | Usage: {1:0.0}%/lap | Remaining: {2} laps", currentFuelPct, consumptionPct, lapsRemaining);
            }
            else
            {
                msg = GetLocalizedMessage("lfs_msg_fuel_status_simple", "^3[CrewChief] Fuel: {0:0.0}% remaining", currentFuelPct);
            }
            SendMessageLocal(msg);
        }

        public class LFSStructWrapper
        {
            public long TicksWhenRead;
            public LFSData Data;

            public LFSStructWrapper Copy()
            {
                return new LFSStructWrapper
                {
                    TicksWhenRead = TicksWhenRead,
                    Data = Data.Copy(),
                };
            }
        }

        public override void Dispose()
        {
            lock (this)
            {
                if (outSimReader != null)
                {
                    outSimReader.Stop();
                    outSimReader = null;
                }

                if (insim != null)
                {
                    if (insim.IsConnected)
                    {
                        insim.Disconnect();
                    }

                    insim = null;
                }

                if (Instance == this)
                {
                    Instance = null;
                }

                if (initialised)
                {
                    initialised = false;
                    Console.WriteLine("Disconnected from LFS InSim");
                }
            }
        }

        public override void DumpRawGameData()
        {
            if (dumpToFile && dataToDump != null && dataToDump.Count > 0 && filenameToDump != null)
            {
                SerializeObject(dataToDump.ToArray<LFSStructWrapper>(), filenameToDump);
            }
        }

        public override object ReadGameData(bool forSpotter)
        {
            lock (this)
            {
                if (!initialised)
                {
                    if (!InitialiseInternal())
                    {
                        throw new GameDataReadException("Failed to read game data - LFS InSim is not initialised");
                    }
                }

                lfsData.TicksWhenRead = DateTime.UtcNow.Ticks;

                if (outSimReader != null && outSimReader.HasData && lfsData.Data != null)
                {
                    lfsData.Data.HasOutSimData = true;
                    lfsData.Data.LastOutSimTime = outSimReader.LastPacketReceived;
                    lfsData.Data.TyreAirTemps[0] = outSimReader.RL_AirTemp;
                    lfsData.Data.TyreAirTemps[1] = outSimReader.RR_AirTemp;
                    lfsData.Data.TyreAirTemps[2] = outSimReader.FL_AirTemp;
                    lfsData.Data.TyreAirTemps[3] = outSimReader.FR_AirTemp;
                    lfsData.Data.OutSimCurrentLapDist = outSimReader.CurrentLapDist;
                }
                else if (lfsData.Data != null)
                {
                    lfsData.Data.HasOutSimData = false;
                }

                if (dumpToFile)
                {
                    dataToDump.Add(lfsData.Copy());
                }

                return lfsData;
            }
        }

        public override TracedGameData ReadGameDataFromFile(string filename, int pauseBeforeStart)
        {
            if (dataReadFromFile == null || filename != lastReadFileName)
            {
                dataReadFromFileIndex = 0;
                string filePathResolved = Utilities.ResolveDataFile(dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<LFSStructWrapper[]>(filePathResolved);
                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }

            if (dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex)
            {
                LFSStructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex];
                workingData = structWrapperData;
                dataReadFromFileIndex++;
                return new TracedGameData
                {
                    GameData = structWrapperData,
                    TicksWhenRead = structWrapperData.TicksWhenRead,
                };
            }
            else
            {
                return null;
            }
        }

        public override void ResetGameDataFromFile()
        {
            dataReadFromFileIndex = 0;
        }

        protected override bool InitialiseInternal()
        {
            dataToDump = new List<LFSStructWrapper>();
            lock (this)
            {
                if (insim != null && initialised)
                {
                    return true;
                }

                try
                {
                    lfsData.Data = new LFSData();
                    insim = new InSim();

                    insim.InSimError += InSimError;
                    insim.Bind<IS_VER>(Version);

                    insim.Bind<IS_STA>(State);
                    insim.Bind<IS_TINY>(Tiny);

                    insim.Bind<IS_ISM>(MultiplayerStarted);

                    insim.Bind<IS_MCI>(MultiCarInfo);

                    insim.Bind<IS_NCN>(NewConnection);
                    insim.Bind<IS_CNL>(ConnectionLeave);
                    insim.Bind<IS_NPL>(NewPlayer);
                    insim.Bind<IS_PLL>(PlayerLeave);
                    insim.Bind<IS_CPR>(PlayerRename);
                    insim.Bind<IS_TOC>(TakeOverCar);

                    insim.Bind<IS_RST>(RaceStart);
                    insim.Bind<IS_LAP>(LapTime);
                    insim.Bind<IS_SPX>(SplitTime);
                    insim.Bind<IS_CRS>(CarReset);
                    insim.Bind<IS_REO>(Reorder);

                    insim.Bind<IS_PLA>(PitLane);
                    insim.Bind<IS_PIT>(PitStop);

                    insim.Bind<IS_FLG>(Flag);
                    insim.Bind<IS_CON>(CarContact);
                    insim.Bind<IS_OBH>(ObjectHit);
                    insim.Bind<IS_HLV>(HotLapValidity);
                    insim.Bind<IS_PEN>(Penalty);
                    insim.Bind<IS_FIN>(RaceFinished);
                    insim.Bind<IS_SMALL>(Small);

                    insim.Disconnected += (s, e) =>
                    {
                        initialised = false;
                        if (outSimReader != null)
                        {
                            outSimReader.Stop();
                        }
                        Console.WriteLine("LFS InSim disconnected: " + (e != null ? e.Reason.ToString() : "socket closed"));
                    };

                    int insimPort = UserSettings.GetUserSettings().getInt("lfs_insim_port");
                    if (insimPort <= 0)
                    {
                        insimPort = 29999;
                    }
                    string insimHost = UserSettings.GetUserSettings().getString("lfs_insim_host");
                    if (string.IsNullOrEmpty(insimHost))
                    {
                        insimHost = "127.0.0.1";
                    }

                    Console.WriteLine($"Attempting to connect to LFS InSim on {insimHost}:{insimPort}...");

                    insim.Initialize(new InSimSettings
                    {
                        Host = insimHost,
                        Port = insimPort,
                        Admin = string.Empty,
                        Flags = InSimFlags.ISF_MCI | InSimFlags.ISF_LOCAL | InSimFlags.ISF_CON | InSimFlags.ISF_OBH | InSimFlags.ISF_HLV,
                        UdpPort = 0,
                        Interval = 40,
                        IName = "Crew Chief V4",
                    });

                    // Wait briefly for handshake (Version packet received from LFS)
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (!initialised && insim.IsConnected && sw.ElapsedMilliseconds < 3000)
                    {
                        Thread.Sleep(20);
                    }

                    if (!initialised)
                    {
                        Console.WriteLine($"LFS InSim connection to {insimHost}:{insimPort} timed out waiting for IS_VER handshake.");
                        if (insim.IsConnected)
                        {
                            insim.Disconnect();
                        }
                        insim = null;
                        return false;
                    }

                    Console.WriteLine("LFS InSim connected and ready.");
                    Instance = this;
                    SendMessageLocal(GetLocalizedMessage("lfs_msg_connected", "^3[CrewChief] Connected to LFS. Note: 1L announced = 1% fuel tank (F12 menu)"));
                    if (outSimReader == null)
                    {
                        outSimReader = new LFSOutSimReader();
                    }
                    outSimReader.Start();
                    return true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to connect via InSim: " + e.Message);
                    if (insim != null)
                    {
                        try { if (insim.IsConnected) insim.Disconnect(); } catch { }
                        insim = null;
                    }
                    initialised = false;
                    return false;
                }
            }
        }

        public override void DisconnectFromProcess()
        {
            Dispose();
        }

        private void Version(InSim inSim, IS_VER ver)
        {
            initialised = true;
            Console.WriteLine("LFS InSim initialised");
            Console.WriteLine("InSim version: {0}", ver.InSimVer);
            Console.WriteLine("LFS version: {0} {1}", ver.Product, ver.Version);

            lfsData.Data.InSimVersion = ver.InSimVer;

            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_SST});
            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_NCN});
            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_NPL});
            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_RST});
            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_GTM});
        }

        private void Small(InSim inSim, IS_SMALL small)
        {
            if (small.SubT == SmallType.SMALL_RTP)
            {
                // UVal is time in hundredths of a second since race/replay start
                long timeInHundredths = small.UVal;
                if (timeInHundredths > 0)
                {
                    double elapsedSeconds = (double)timeInHundredths / 100.0;
                    lfsData.Data.SessionState.GreenTime = DateTime.Now.AddSeconds(-elapsedSeconds);
                    Console.WriteLine($"InSim: Race/Replay elapsed time from LFS: {elapsedSeconds:F2}s");
                }
            }
        }

        private void State(InSim inSim, IS_STA sta)
        {
            var sessionType = (LFSSessionType) sta.RaceInProg;
            SessionState sessionState = lfsData.Data.SessionState;

            if (sta.ReqI == 0)
            {
                if (sessionType == LFSSessionType.NoRace && sessionType != lfsData.Data.SessionState.RaceInProg)
                {
                    Console.WriteLine("InSim: Session ended");
                    sessionState.StartTime = DateTime.MinValue;
                    sessionState.GreenTime = DateTime.MinValue;
                    sessionState.PlayerFinished = false;
                    sessionState.LeaderFinished = false;
                }
            }

            sessionState.StateFlags = sta.Flags;

            if (sta.ViewPLID != 0 && sessionState.ViewPLID != sta.ViewPLID &&
                lfsData.Data.Players.ContainsKey(sta.ViewPLID))
            {
                Console.WriteLine($"Viewing player: {lfsData.Data.Players[sta.ViewPLID].PName}");
            }

            sessionState.ViewPLID = sta.ViewPLID;

            sessionState.QualMins = sta.QualMins;
            sessionState.RaceLaps = sta.RaceLaps;
            sessionState.RaceInProg = sessionType;

            sessionState.Track = sta.Track;
            sessionState.Wind = (WindStrength) sta.Wind;

            // If a replay is running or we joined mid-race, query start/green time from LFS
            // Note: During a live race start on the grid, do NOT send TINY_GTM because receiving SMALL_RTP prematurely
            // sets GreenTime and skips the Countdown phase needed for pre-lights and "Go Go Go" announcements.
            bool midRaceOrReplay = sta.Flags.HasFlag(StateFlags.ISS_REPLAY) ||
                (sta.RaceInProg != 0 && sessionState.StartTime != DateTime.MinValue &&
                 lfsData.Data.Players.Values.Any(p => p.Scoring != null && (p.Scoring.Speed > 910 || p.Scoring.LapData.Count > 0 || p.Scoring.Split > 0)));

            if (midRaceOrReplay && (sessionState.GreenTime == DateTime.MinValue || sessionState.GreenTime.Ticks == 0))
            {
                insim.Send(new IS_TINY { ReqI = 1, SubT = TinyType.TINY_GTM });
                insim.Send(new IS_TINY { ReqI = 1, SubT = TinyType.TINY_RST });
            }
        }

        private void Tiny(InSim inSim, IS_TINY tiny)
        {
            if (tiny.SubT == TinyType.TINY_CLR)
            {
                lfsData.Data.Players.Clear();
            }
        }

        private void MultiplayerStarted(InSim inSim, IS_ISM ism)
        {
            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_NPL});
            insim.Send(new IS_TINY {ReqI = 1, SubT = TinyType.TINY_NCN});
        }

        private void MultiCarInfo(InSim inSim, IS_MCI mci)
        {
            foreach (CompCar compCar in mci.Info)
            {
                Player player = lfsData.Data.GetPlayerByPLID(compCar.PLID);

                if (player == null)
                {
                    player = new Player
                    {
                        PLID = compCar.PLID,
                        PName = $"Car {compCar.PLID}",
                        CName = "Unknown",
                        Scoring = new PlayerScoring(),
                        PitData = new PitData(),
                    };
                    lfsData.Data.Players[compCar.PLID] = player;
                }

                player.Scoring.Node = compCar.Node;
                player.Scoring.Position = compCar.Position;
                player.Scoring.Info = compCar.Info;
                player.Scoring.X = compCar.X;
                player.Scoring.Y = compCar.Y;
                player.Scoring.Z = compCar.Z;
                player.Scoring.Speed = compCar.Speed;
                player.Scoring.Direction = compCar.Direction;
                player.Scoring.Heading = compCar.Heading;
                player.Scoring.AngVel = compCar.AngVel;
            }
        }

        private void NewConnection(InSim inSim, IS_NCN ncn)
        {
            Console.WriteLine($"InSim: New connection: {ncn.UCID} - {ncn.PName} ({ncn.UName})");

            // TODO: Handle going from menu to multiplayer
            if (!lfsData.Data.Connections.ContainsKey(ncn.UCID))
            {
                lfsData.Data.Connections.Add(ncn.UCID, new Connection
                {
                    UCID = ncn.UCID,
                    PName = ncn.PName,
                    UName = ncn.UName,
                });
            }

            LogAllConnections();
        }

        private void ConnectionLeave(InSim inSim, IS_CNL cnl)
        {
            Console.WriteLine($"InSim: Connection left: {cnl.UCID}");

            if (lfsData.Data.Connections.ContainsKey(cnl.UCID))
            {
                lfsData.Data.Connections.Remove(cnl.UCID);
            }

            LogAllConnections();
        }

        private void NewPlayer(InSim inSim, IS_NPL npl)
        {
            Console.WriteLine($"InSim: New player: {npl.PLID} - {npl.PName}", npl.PLID);

            Connection connection = null;
            if (lfsData.Data.Connections.ContainsKey(npl.UCID))
            {
                connection = lfsData.Data.Connections[npl.UCID];
            }

            if (lfsData.Data.Players.ContainsKey(npl.PLID))
            {
                // Existing player leaving garage or updating
                Player player = lfsData.Data.Players[npl.PLID];
                player.PType = npl.PType;
                player.CName = npl.CName;
                player.PName = npl.PName;
                if (npl.Fuel != 255)
                {
                    player.InitialFuel = npl.Fuel;
                    player.Fuel200 = (byte)(npl.Fuel * 2);
                }
                player.ResetOnSessionStart();
                if (connection != null)
                {
                    player.Connection = connection;
                }
                if (npl.PLID == lfsData.Data.SessionState.ViewPLID)
                {
                    lfsData.Data.SessionState.PlayerFinished = false;
                }
            }
            else
            {
                // New player joining track
                lfsData.Data.Players.Add(npl.PLID, new Player
                {
                    PLID = npl.PLID,
                    PType = npl.PType,
                    PName = npl.PName,
                    CName = npl.CName,
                    Connection = connection,
                    InitialFuel = npl.Fuel,
                    Fuel200 = npl.Fuel != 255 ? (byte)(npl.Fuel * 2) : (byte)255,
                    Scoring = new PlayerScoring(),
                    PitData = new PitData(),
                });
            }

            Console.WriteLine($"InSim: Player {npl.PLID} ({npl.PName}) initial fuel: {(npl.Fuel != 255 ? npl.Fuel.ToString() + "%" : "not available")}");

            LogAllPlayers();
            LogAllConnections();
        }

        private void PlayerLeave(InSim inSim, IS_PLL pll)
        {
            Console.WriteLine($"InSim: Player left: {pll.PLID}");

            if (lfsData.Data.Players.ContainsKey(pll.PLID))
            {
                lfsData.Data.Players.Remove(pll.PLID);
            }

            LogAllPlayers();
        }

        private void PlayerRename(InSim inSim, IS_CPR cpr)
        {
            Console.WriteLine($"InSim: Connection player renamed: {cpr.UCID}");

            if (lfsData.Data.Connections.ContainsKey(cpr.UCID))
            {
                lfsData.Data.Connections[cpr.UCID].PName = cpr.PName;
            }

            // Rename only human players
            foreach (var playerEntry in lfsData.Data.Players.Where(pl =>
                pl.Value.Connection != null && pl.Value.Connection.UCID == cpr.UCID && (pl.Value.PType & PlayerTypes.PLT_AI) == 0).ToList())
            {
                Console.WriteLine($"InSim: Rename human player #{cpr.UCID}: {playerEntry.Value.PName} => {cpr.PName}");

                lfsData.Data.Players[playerEntry.Value.PLID].PName = cpr.PName;
            }

            LogAllPlayers();
            LogAllConnections();
        }

        private void TakeOverCar(InSim inSim, IS_TOC toc)
        {
            Console.WriteLine($"InSim: Take over car #{toc.PLID}: {toc.OldUCID} => {toc.NewUCID}");

            var players = lfsData.Data.Players;
            var connections = lfsData.Data.Connections;

            if (players.ContainsKey(toc.PLID) && connections.ContainsKey(toc.NewUCID))
            {
                players[toc.PLID].Connection = connections[toc.NewUCID];
                players[toc.PLID].PName = connections[toc.NewUCID].PName;
            }

            LogAllPlayers();
        }

        /// <summary>
        /// Handles IS_RST packet sent by LFS when a new session or race begins.
        /// Resets session state, lap counters, track cut warnings, and notifies players.
        /// </summary>
        private void RaceStart(InSim inSim, IS_RST rst)
        {
            Console.WriteLine("InSim: Session started");

            SessionState sessionState = lfsData.Data.SessionState;

            sessionState.StartTime = DateTime.Now;
            sessionState.GreenTime = DateTime.MinValue;
            sessionState.QualMins = rst.QualMins;
            sessionState.RaceLaps = rst.RaceLaps;
            sessionState.RaceInProg = rst.QualMins == 0 ? LFSSessionType.RaceOrPractice : LFSSessionType.Qualifying;
            sessionState.SessionFlags = rst.Flags;
            sessionState.Track = rst.Track;
            sessionState.PlayerFinished = false;
            sessionState.LeaderFinished = false;
            sessionState.NumNodes = rst.NumNodes;
            sessionState.Split1 = rst.Split1;
            sessionState.Split2 = rst.Split2;
            sessionState.Split3 = rst.Split3;

            lfsData.Data.CutTrackWarnings = 0;
            lfsData.Data.LastIncidentTime = DateTime.MinValue;
            lfsData.Data.WavedFlag = new WavedFlag();

            foreach (var player in lfsData.Data.Players.Values)
            {
                player.ResetOnSessionStart();
            }

            bool fuelAllowed = rst.Flags.HasFlag(RaceFlags.HOSTF_SHOW_FUEL);
            if (fuelAllowed)
            {
                SendMessageLocal(GetLocalizedMessage("lfs_msg_session_started", "^3[CrewChief] Session started. Note: 1L announced = 1% fuel tank (F12 menu)"));
            }
            else
            {
                Console.WriteLine("InSim: Host has disabled fuel info (/showfuel no). Fuel tracking will be inactive.");
                SendMessageLocal(GetLocalizedMessage("lfs_msg_session_no_fuel", "^1[CrewChief] Session started. Note: Fuel telemetry disabled by host (/showfuel no)"));
            }
        }

        /// <summary>
        /// Handles IS_LAP packet sent when a player crosses the start/finish line to complete a lap.
        /// Records lap time, split breakdown, and fuel percentage at the line.
        /// </summary>
        private void LapTime(InSim inSim, IS_LAP lap)
        {
            if (!lfsData.Data.Players.ContainsKey(lap.PLID))
                return;

            Player player = lfsData.Data.Players[lap.PLID];
            PlayerScoring scoring = player.Scoring;

            if (lap.Fuel200 != 255)
            {
                player.Fuel200 = lap.Fuel200;
            }
            player.Penalty = lap.Penalty;
            scoring.LapsDone = lap.LapsDone;
            if (player.PitData != null)
            {
                player.PitData.NumStops = lap.NumStops;
            }

            scoring.LapData.Add(new Lap
            {
                ETime = lap.ETime,
                LTime = lap.LTime,
                Split1 = scoring.Split1,
                Split2 = scoring.Split2,
                Split3 = scoring.Split3,
            });

            scoring.Split = 0;
            scoring.Split1 = new Split();
            scoring.Split2 = new Split();
            scoring.Split3 = new Split();

            lfsData.Data.SessionState.GreenTime = DateTime.Now.Subtract(lap.ETime);
        }

        /// <summary>
        /// Handles IS_SPX packet sent when a player crosses an intermediate sector split.
        /// </summary>
        private void SplitTime(InSim inSim, IS_SPX spx)
        {
            if (!lfsData.Data.Players.ContainsKey(spx.PLID))
                return;

            Player player = lfsData.Data.Players[spx.PLID];
            PlayerScoring scoring = player.Scoring;

            if (spx.Fuel200 != 255)
            {
                player.Fuel200 = spx.Fuel200;
            }
            player.Penalty = spx.Penalty;
            if (player.PitData != null)
            {
                player.PitData.NumStops = spx.NumStops;
            }

            lfsData.Data.SessionState.GreenTime = DateTime.Now.Subtract(spx.ETime);

            scoring.Split = spx.Split;

            switch (spx.Split)
            {
                case 1:
                    scoring.Split1.STime = spx.STime;
                    scoring.Split1.ETime = spx.ETime;
                    break;
                case 2:
                    scoring.Split2.STime = spx.STime;
                    scoring.Split2.ETime = spx.ETime;
                    break;
                case 3:
                    scoring.Split3.STime = spx.STime;
                    scoring.Split3.ETime = spx.ETime;
                    break;
                default:
                    Console.WriteLine($"Invalid split number: {spx.Split}");
                    return;
            }
        }

        /// <summary>
        /// Handles IS_PLA packet sent when a car enters or exits the pit lane.
        /// </summary>
        private void PitLane(InSim inSim, IS_PLA pla)
        {
            Player player = lfsData.Data.GetPlayerByPLID(pla.PLID);

            player.PitData.PitLaneFact = pla.Fact;

            if (pla.Fact == PitLaneFact.PITLANE_ENTER)
            {
                Console.WriteLine($"{player.PName} entered pit lane");
            }

            if (pla.Fact == PitLaneFact.PITLANE_EXIT)
            {
                Console.WriteLine($"{player.PName} exited pit lane");
            }
        }

        /// <summary>
        /// Handles IS_PIT packet sent when a car finishes servicing in the pit box.
        /// </summary>
        private void PitStop(InSim inSim, IS_PIT pit)
        {
            Player player = lfsData.Data.GetPlayerByPLID(pit.PLID);

            Console.WriteLine($"{player.PName} - number of pit stops: {pit.NumStops}");

            player.PitData.NumStops = pit.NumStops;
        }

        /// <summary>
        /// Handles IS_FLG packet indicating yellow flag, blue flag, or track condition changes.
        /// </summary>
        private void Flag(InSim inSim, IS_FLG flg)
        {
            lfsData.Data.WavedFlag = new WavedFlag()
            {
                PLID = flg.PLID,
                IsOn = flg.OffOn,
                Flag = flg.Flag,
                CarBehind = flg.CarBehind
            };
        }

        /// <summary>
        /// Handles IS_CON packet for car-to-car collisions, updating incident tracking.
        /// </summary>
        private void CarContact(InSim inSim, IS_CON con)
        {
            byte viewPLID = lfsData.Data.SessionState.ViewPLID;
            if (con.A.PLID == viewPLID || con.B.PLID == viewPLID)
            {
                Console.WriteLine($"InSim: Player collision! SpClose: {con.SpClose}");
                lfsData.Data.LastIncidentTime = DateTime.UtcNow;
                lfsData.Data.LastIncidentPLID = viewPLID;
                lfsData.Data.LastIncidentSpClose = con.SpClose;
            }
            else
            {
                lfsData.Data.LastIncidentTime = DateTime.UtcNow;
                lfsData.Data.LastIncidentPLID = con.A.PLID;
                lfsData.Data.LastIncidentSpClose = con.SpClose;
            }
        }

        /// <summary>
        /// Handles IS_OBH packet for collisions with environment or layout objects.
        /// </summary>
        private void ObjectHit(InSim inSim, IS_OBH obh)
        {
            byte viewPLID = lfsData.Data.SessionState.ViewPLID;
            if (obh.PLID == viewPLID)
            {
                Console.WriteLine($"InSim: Player hit object! SpClose: {obh.SpClose}");
                lfsData.Data.LastIncidentTime = DateTime.UtcNow;
                lfsData.Data.LastIncidentPLID = viewPLID;
                lfsData.Data.LastIncidentSpClose = obh.SpClose;
            }
        }

        /// <summary>
        /// Handles IS_HLV packet for track limits and hotlap validity infringements.
        /// </summary>
        private void HotLapValidity(InSim inSim, IS_HLV hlv)
        {
            byte viewPLID = lfsData.Data.SessionState.ViewPLID;
            if (hlv.PLID == viewPLID)
            {
                Console.WriteLine($"InSim: Player track limits violation: {hlv.HLVC}");
                lfsData.Data.CutTrackWarnings++;
            }
        }

        /// <summary>
        /// Handles IS_PEN packet when a penalty (Drive-Through, Stop &amp; Go, 30s) is assigned or cleared.
        /// </summary>
        private void Penalty(InSim inSim, IS_PEN pen)
        {
            if (lfsData.Data.Players.ContainsKey(pen.PLID))
            {
                Player player = lfsData.Data.Players[pen.PLID];
                player.Penalty = pen.NewPen;
                Console.WriteLine($"InSim: Player {player.PName} penalty: {pen.NewPen} (Reason: {pen.Reason})");
            }
        }

        /// <summary>
        /// Handles IS_FIN packet sent when the player or another racer completes the race.
        /// Triggers checkered flag and finished session phase transitions.
        /// </summary>
        private void RaceFinished(InSim inSim, IS_FIN fin)
        {
            byte viewPLID = lfsData.Data.SessionState.ViewPLID;
            if (fin.PLID == viewPLID)
            {
                Console.WriteLine($"InSim: Viewed player finished race! Pos: {fin.Confirm}");
                lfsData.Data.SessionState.PlayerFinished = true;
            }

            Player leader = lfsData.Data.Players.Values.FirstOrDefault(p => p.Scoring != null && p.Scoring.Position == 1);
            if (leader != null && fin.PLID == leader.PLID)
            {
                Console.WriteLine("InSim: Leader finished race!");
                lfsData.Data.SessionState.LeaderFinished = true;
            }
        }

        /// <summary>
        /// Handles IS_CRS packet when a car is reset or teleports to pit, clearing stale lap states.
        /// </summary>
        private void CarReset(InSim inSim, IS_CRS crs)
        {
            Console.WriteLine($"InSim: Car reset PLID {crs.PLID}");
            if (lfsData.Data.Players.ContainsKey(crs.PLID))
            {
                lfsData.Data.Players[crs.PLID].ResetOnSessionStart();
            }
            byte viewPLID = lfsData.Data.SessionState.ViewPLID;
            if (crs.PLID == viewPLID)
            {
                lfsData.Data.SessionState.PlayerFinished = false;
            }
        }

        /// <summary>
        /// Handles IS_REO packet when the starting grid is reordered or race is restarted.
        /// </summary>
        private void Reorder(InSim inSim, IS_REO reo)
        {
            Console.WriteLine("InSim: Grid reordered");
            SessionState sessionState = lfsData.Data.SessionState;
            sessionState.StartTime = DateTime.Now;
            sessionState.GreenTime = DateTime.MinValue;
            sessionState.PlayerFinished = false;
            sessionState.LeaderFinished = false;
            foreach (var player in lfsData.Data.Players.Values)
            {
                player.ResetOnSessionStart();
            }
        }

        private void LogAllConnections()
        {
            Console.WriteLine("InSim: Connections");

            foreach (var connectionEntry in lfsData.Data.Connections)
            {
                Console.WriteLine(
                    $"- {connectionEntry.Value.UCID}: {connectionEntry.Value.PName} ({connectionEntry.Value.UName})");
            }
        }

        private void LogAllPlayers()
        {
            Console.WriteLine("InSim: Players");

            foreach (var playerEntry in lfsData.Data.Players)
            {
                string uName = playerEntry.Value.Connection != null ? playerEntry.Value.Connection.UName : "AI/Local";
                Console.WriteLine(
                    $"- {playerEntry.Value.PLID}: {playerEntry.Value.PName} ({uName}) - {playerEntry.Value.CName}");
            }
        }

        private void InSimError(object sender, InSimErrorEventArgs e)
        {
            initialised = false;
            Console.WriteLine("InSim Error: {0}", e.Exception.Message);
        }
    }
}