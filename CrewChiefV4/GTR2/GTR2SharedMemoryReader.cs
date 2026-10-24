//#define TRACE_BUFFER_READ_ELAPSED_TIME

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

using CrewChiefV4.GTR2;
using GTR2SharedMemory;
using GTR2SharedMemory.GTR2Data;
using rF2SharedMemory;

namespace CrewChiefV4.GTR2
{
    public class GTR2SharedMemoryReader : GameDataReader
    {
        MappedBuffer<GTR2Telemetry> telemetryBuffer = new MappedBuffer<GTR2Telemetry>(GTR2Constants.MM_TELEMETRY_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<GTR2Scoring> scoringBuffer = new MappedBuffer<GTR2Scoring>(GTR2Constants.MM_SCORING_FILE_NAME, true /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<GTR2Extended> extendedBuffer = new MappedBuffer<GTR2Extended>(GTR2Constants.MM_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);

        private bool initialised = false;
        private List<GTR2StructWrapper> dataToDump;
        private GTR2StructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private string lastReadFileName = null;

        // Capture mCurrentET from scoring to prevent dumping double frames to the file.
        private double lastScoringET = -1.0;
        // Capture mElapsedTime from telemetry of the first vehicle to prevent dumping double frames to the file.
        private uint lastTelemetryVersionUpdateBegin = 0;

        public class GTR2StructWrapper
        {
            public long ticksWhenRead;
            public GTR2Telemetry telemetry;
            public GTR2Scoring scoring;
            public GTR2Extended extended;
        }

        public override void DumpRawGameData()
        {
            if (this.dumpToFile && this.dataToDump != null && this.dataToDump.Count > 0 && this.filenameToDump != null)
            {
                this.SerializeObject(this.dataToDump.ToArray<GTR2StructWrapper>(), this.filenameToDump);

            }
        }

        public override void ResetGameDataFromFile()
        {
            this.dataReadFromFileIndex = 0;
        }

        public override TracedGameData ReadGameDataFromFile(String filename, int pauseBeforeStart)
        {
            if (this.dataReadFromFile == null || filename != this.lastReadFileName)
            {
                this.dataReadFromFileIndex = 0;

                var filePathResolved = Utilities.ResolveDataFile(this.dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<GTR2StructWrapper[]>(filePathResolved);

                this.lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }
            if (dataReadFromFile != null && dataReadFromFile.Length > this.dataReadFromFileIndex)
            {
                GTR2StructWrapper structWrapperData = dataReadFromFile[this.dataReadFromFileIndex];
                this.dataReadFromFileIndex++;
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

        protected override Boolean InitialiseInternal()
        {
            this.lastScoringET = -1.0;

            // This needs to be synchronized, because disconnection happens from CrewChief.Run and MainWindow.Dispose.
            lock (this)
            {
                if (!this.initialised)
                {
                    try
                    {
                        this.telemetryBuffer.Connect();
                        this.scoringBuffer.Connect();
                        this.extendedBuffer.Connect();

                        // Clear mapped views.
                        this.telemetry = new GTR2Telemetry();
                        this.scoring = new GTR2Scoring();
                        this.extended = new GTR2Extended();

                        if (dumpToFile)
                            this.dataToDump = new List<GTR2StructWrapper>();

                        this.initialised = true;

                        Console.WriteLine("Initialized GTR 2 Shared Memory");
                    }
                    catch (Exception)
                    {
                        this.initialised = false;
                        this.DisconnectInternal();
                    }
                }
                return this.initialised;
            }
        }

        // Marshalled views:
        private GTR2Telemetry telemetry;
        private GTR2Scoring scoring;
        private GTR2Extended extended;

        public override Object ReadGameData(Boolean forSpotter)
        {
            lock (this)
            {
                if (!this.initialised)
                {
                    if (!this.InitialiseInternal())
                    {
                        throw new GameDataReadException("Failed to initialise shared memory");
                    }
                }
                try 
                {
#if TRACE_BUFFER_READ_ELAPSED_TIME
                    var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
                    extendedBuffer.GetMappedData(ref this.extended);
                    telemetryBuffer.GetMappedData(ref this.telemetry);

                    // Scoring is the most important game data in Crew Chief sense, 
                    // so acquire it last, hoping it will be most recent view of all buffer types.
                    scoringBuffer.GetMappedData(ref this.scoring);

                    // Create a new copy marshalled views.  Thia is necessary because core code caches states, so each
                    // state has to be an individual object.  We can't avoid copy by marshalling directly into wrapper,
                    // because not all marshalling calls fetch new buffer.
                    var wrapper = new GTR2StructWrapper()
                    {
                        extended = this.extended,
                        telemetry = this.telemetry,
                        scoring = this.scoring,
                        ticksWhenRead = DateTime.UtcNow.Ticks
                    };

                    if (!forSpotter && currentlyTracingGameData && this.dataToDump != null)
                    {
                        // Note: this is lossy save, because we only save update if Telemtry or Scoring changed.
                        // Other buffers don't change that much, so it should be fine.

                        // Exclude empty frames.
                        if (wrapper.scoring.mScoringInfo.mNumVehicles > 0
                            && wrapper.extended.mSessionStarted == 1)
                        {
                            var hasTelemetryChanged = wrapper.telemetry.mVersionUpdateBegin != this.lastTelemetryVersionUpdateBegin;
                            this.lastTelemetryVersionUpdateBegin = wrapper.telemetry.mVersionUpdateBegin;

                            var currScoringET = wrapper.scoring.mScoringInfo.mCurrentET;
                            if (currScoringET != this.lastScoringET  // scoring contains new payload
                                || hasTelemetryChanged)  // Or, telemetry updated.
                            {
                                // NOTE: truncation code could be moved to DumpRawGameData method for reduced CPU use.
                                // However, this causes memory pressure (~250Mb/minute with 22 vehicles), so probably better done here.
                                wrapper.scoring.mVehicles = this.GetPopulatedVehicleInfoArray<GTR2VehicleScoring>(wrapper.scoring.mVehicles, wrapper.scoring.mScoringInfo.mNumVehicles);

                                int maxmID = 0;
                                foreach (var vehicleScoring in wrapper.scoring.mVehicles)
                                    maxmID = Math.Max(maxmID, vehicleScoring.mID);

                                if (maxmID < GTR2Constants.MAX_MAPPED_IDS)
                                {
                                    // Since serialization to XML produces a lot of useless tags even for small arrays, truncate tracked damage array.
                                    // It is indexed by mID.  Max mID in current set is equal to mNumVehicles in 99% of cases, so just truncate to this size.
                                    wrapper.extended.mTrackedDamages = this.GetPopulatedVehicleInfoArray<GTR2TrackedDamage>(wrapper.extended.mTrackedDamages, maxmID + 1);
                                }

                                this.dataToDump.Add(wrapper);
                                this.lastScoringET = currScoringET;
                            }
                        }
                    }

#if TRACE_BUFFER_READ_ELAPSED_TIME
                    watch.Stop();
                    var microseconds = watch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency;
                    System.Console.WriteLine("Buffer read microseconds: " + microseconds);
#endif
                    return wrapper;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("GTR2 Shared Memory connection failed.");
                    this.DisconnectInternal();
                    throw new GameDataReadException(ex.Message, ex);
                }
            }
        }

        private VehicleInfoT[] GetPopulatedVehicleInfoArray<VehicleInfoT>(VehicleInfoT[] vehicles, int numPopulated)
        {
            // To reduce serialized size, only return non-empty vehicles.
            var populated = new List<VehicleInfoT>();
            for (int i = 0; i < numPopulated; ++i)
                populated.Add(vehicles[i]);

            return populated.ToArray();
        }

        public override void DisconnectFromProcess()
        {
            var wasInitialised = this.initialised;
            if (wasInitialised)
            {
                Console.WriteLine("Telemetry: " + this.telemetryBuffer.GetStats());
                Console.WriteLine("Scoring: " + this.scoringBuffer.GetStats());
                Console.WriteLine("Extended: " + this.extendedBuffer.GetStats());
            }

            this.DisconnectInternal();

            // There's still possibility of double message, but who cares.
            if (wasInitialised)
                Console.WriteLine("Disconnected from GTR 2 Shared Memory");

            // Hack to re-check plugin version.
            GTR2GameStateMapper.pluginVerified = false;
        }

        private void DisconnectInternal()
        {
            this.initialised = false;

            this.telemetryBuffer.Disconnect();
            this.scoringBuffer.Disconnect();
            this.extendedBuffer.Disconnect();
        }

        public override void Dispose()
        {
            try
            {
                this.DisconnectInternal();
            }
            catch (Exception e) {Log.Exception(e);}
        }
    }
}
