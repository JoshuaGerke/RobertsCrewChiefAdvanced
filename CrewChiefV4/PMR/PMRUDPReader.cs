using System;
using System.Collections.Generic;
using System.Threading;

namespace CrewChiefV4.PMR
{
    /// <summary>
    /// Reads Project Motor Racing data from the PMR UDP telemetry feed and exposes it
    /// to Crew Chief in a format that a GameStateMapper can consume.
    /// 
    /// Uses the PMR
    /// UDP protocol (UDPRaceInfo / UDPParticipantRaceState / UDPVehicleTelemetry).
    /// </summary>
    public class PMRUDPReader : GameDataReader
    {
        /// <summary>
        /// Wrapper that gets passed to the GameStateMapper.
        /// It contains a snapshot of the race info, leaderboard and player telemetry.
        /// </summary>
        [Serializable]
        public class PMRStructWrapper
        {
            /// <summary>
            /// Tick timestamp (DateTime.UtcNow.Ticks) when this snapshot was taken.
            /// </summary>
            public long ticksWhenRead;

            /// <summary>
            /// Overall race/session metadata (track, weather, session type, etc.).
            /// </summary>
            public UDPRaceInfo RaceInfo;

            /// <summary>
            /// Sorted leaderboard entries (all participants).
            /// </summary>
            public List<UDPParticipantRaceState> Leaderboard;

            /// <summary>
            /// The player’s current race-state (lap/sector/position).
            /// </summary>
            public UDPParticipantRaceState PlayerRaceState;

            /// <summary>
            /// The player’s telemetry (speed, gear, engine, chassis, etc.).
            /// </summary>
            public UDPVehicleTelemetry PlayerTelemetry;

            public List<UDPVehicleTelemetry> AllTelemetry;
        }

        private readonly int udpPort = UserSettings.GetUserSettings().getInt("pmr_udp_port");

        // PMR helper objects
        private DataStore dataStore;
        private UDPThread udpThread;

        private bool initialised = false;
        private bool running = false;

        // Session recording support (replay of raw game data)
        private List<PMRStructWrapper> dataToDump;
        private PMRStructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private string lastReadFileName = null;

        /// <summary>
        /// Initialise the UDP listener and DataStore.
        /// This is called via GameDataReader.Initialise(), which already does
        /// the "only once" guarding and file-path setup.
        /// </summary>
        protected override bool InitialiseInternal()
        {
            lock (this)
            {
                if (initialised)
                    return true;

                try
                {
                    // Underlying PMR buffer for all incoming packets
                    dataStore = new DataStore();

                    // Build UDPThread arguments. The PMR sample expects simple key=value
                    // tokens: "multicast", "port", "multicast_group".
                    // Defaults in the sample are multicast=true, port=7576, group=224.0.0.150.
                    // Here we:
                    // - honour Crew Chief's udp_data_port
                    // - stick with multicast on the default PMR group
                    var args = new List<string>
                    {
                        "multicast=true",
                        "port=" + udpPort,
                        "multicast_group=224.0.0.150"
                    };

                    // UDPThread runs its own blocking Receive loop on a background thread.
                    udpThread = new UDPThread(dataStore, args.ToArray());
                    udpThread.startThread();

                    if (dumpToFile)
                    {
                        dataToDump = new List<PMRStructWrapper>();
                    }

                    running = true;
                    initialised = true;

                    Console.WriteLine("Initialised PMR UDP listener on port " + udpPort + " (multicast 224.0.0.150)");
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "Error initialising PMR UDP listener");
                    running = false;
                    initialised = false;
                }

                return initialised;
            }
        }

        /// <summary>
        /// Read a single snapshot of raw game data (PMRStructWrapper).
        /// This is called from the main Crew Chief run loop, and also from the
        /// spotter path (forSpotter=true). For PMR we always return the latest
        /// snapshot; the mapper is responsible for handling invalid / incomplete data.
        /// </summary>
        /// <param name="forSpotter">If true, the caller is the spotter pipeline.</param>
        /// <returns>PMRStructWrapper or null if the feed is inactive.</returns>
        public override object ReadGameData(bool forSpotter)
        {
            // Prepare wrapper with a timestamp outside the lock (cheap)
            PMRStructWrapper wrapper = new PMRStructWrapper
            {
                ticksWhenRead = DateTime.UtcNow.Ticks
            };

            lock (this)
            {
                if (!initialised)
                {
                    if (!InitialiseInternal())
                    {
                        throw new GameDataReadException("Failed to initialise PMR UDP listener");
                    }
                }

                if (!running || dataStore == null)
                {
                    return null;
                }

                // If we haven't seen any packets for a while, treat it as "no data".
                // DataStore.timeSinceLastWrite() returns seconds since last packet.
                if (dataStore.timeSinceLastWrite() > 5)
                {
                    return null;
                }

                // Grab a thread-safe snapshot from DataStore.
                UDPRaceInfo raceInfo = dataStore.getRaceInfo();
                if (raceInfo == null)
                {
                    // We haven't yet received a race definition packet; nothing useful to map.
                    return null;
                }

                List<UDPParticipantRaceState> leaderboard = dataStore.getLeaderboard();
                UDPParticipantRaceState playerState = null;
                UDPVehicleTelemetry playerTelemetry = null;

                if (leaderboard != null)
                {
                    // PMR protocol marks the player explicitly.
                    playerState = leaderboard.Find(p => p.m_isPlayer);
                }

                if (playerState != null)
                {
                    // Match telemetry by vehicle ID; DataStore already keeps full list of telemetry packets.
                    playerTelemetry = dataStore.getTelemetryForVehicle(playerState.m_vehicleId);
                }

                wrapper.RaceInfo = raceInfo;
                wrapper.Leaderboard = leaderboard;
                wrapper.PlayerRaceState = playerState;
                wrapper.PlayerTelemetry = playerTelemetry;
                wrapper.AllTelemetry = dataStore.getAllTelemetry();

                // Only dump "full" samples, i.e. we at least know the track.
                if (!forSpotter && currentlyTracingGameData && dataToDump != null)
                {
                    dataToDump.Add(wrapper);
                }
            }

            return wrapper;
        }

        /// <summary>
        /// Persist captured raw data to a compressed trace file (session recording).
        /// This matches the behaviour of PCars2UDPreader and PCars2SharedMemoryReader.
        /// </summary>
        public override void DumpRawGameData()
        {
            if (dumpToFile && dataToDump != null && dataToDump.Count > 0 && filenameToDump != null)
            {
                SerializeObject(dataToDump.ToArray(), filenameToDump);
            }
        }

        /// <summary>
        /// Reset playback index when replaying raw game data from a trace file.
        /// </summary>
        public override void ResetGameDataFromFile()
        {
            dataReadFromFileIndex = 0;
        }

        /// <summary>
        /// Replay raw PMR data from a previously-recorded trace file.
        /// The format matches other games: a zipped JSON array of PMRStructWrapper.
        /// </summary>
        /// <param name="filename">File name (resolved relative to dataFilesPath).</param>
        /// <param name="pauseBeforeStart">Initial delay in ms on first call.</param>
        public override TracedGameData ReadGameDataFromFile(string filename, int pauseBeforeStart)
        {
            if (dataReadFromFile == null || filename != lastReadFileName)
            {
                dataReadFromFileIndex = 0;
                var filePathResolved = Utilities.ResolveDataFile(this.dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<PMRStructWrapper[]>(filePathResolved);
                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }

            if (dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex)
            {
                PMRStructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex++];
                return new TracedGameData
                {
                    GameData = structWrapperData,
                    TicksWhenRead = structWrapperData.ticksWhenRead
                };
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Stop listening to the PMR UDP multicast and shut down the background thread.
        /// </summary>
        public override void stop()
        {
            lock (this)
            {
                running = false;

                try
                {
                    if (udpThread != null)
                    {
                        // PMR sample exposes shutdown(Object, EventArgs)
                        udpThread.shutdown(null, EventArgs.Empty);
                        udpThread = null;
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "Error shutting down PMR UDP listener");
                }

                initialised = false;
            }
        }

        /// <summary>
        /// Dispose pattern: for GameDataReader this just defers to stop().
        /// </summary>
        public override void Dispose()
        {
            stop();
        }
    }
}
