//#define TRACE_BUFFER_READ_ELAPSED_TIME

using CrewChiefV4.RBR;
using CrewChiefV4.RBR.RBRData;
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

namespace CrewChiefV4.RBR
{
    public class RBRSharedMemoryReader : GameDataReader
    {
        private class MappedBuffer<MappedBufferT>
        {
            const int NUM_MAX_RETRIEES = 10;
            int RBR_BUFFER_VERSION_BLOCK_SIZE_BYTES = Marshal.SizeOf(typeof(RBRMappedBufferVersionBlock));
            int RBR_BUFFER_VERSION_BLOCK_WITH_SIZE_SIZE_BYTES = Marshal.SizeOf(typeof(RBRMappedBufferVersionBlockWithSize));

            int BUFFER_SIZE_BYTES;
            string BUFFER_NAME;

            // Holds the entire byte array that can be marshalled to a MappedBufferT.  Partial updates
            // only read changed part of buffer, ignoring trailing uninteresting bytes.  However,
            // to marshal we still need to supply entire structure size.  So, on update only new bytes are copied.
            byte[] fullSizeBuffer = null;

            MemoryMappedFile memoryMappedFile = null;

            bool partial = false;
            bool skipUnchanged = false;
            public MappedBuffer(string buffName, bool partial, bool skipUnchanged)
            {
                this.BUFFER_SIZE_BYTES = Marshal.SizeOf(typeof(MappedBufferT));
                this.BUFFER_NAME = buffName;
                this.partial = partial;
                this.skipUnchanged = skipUnchanged;
            }

            public void Connect()
            {
                this.memoryMappedFile = MemoryMappedFile.OpenExisting(this.BUFFER_NAME, MemoryMappedFileRights.Read);

                // NOTE: Make sure that BUFFER_SIZE matches the structure size in the plugin (debug mode prints that).
                this.fullSizeBuffer = new byte[this.BUFFER_SIZE_BYTES];
            }

            public void Disconnect()
            {
                if (this.memoryMappedFile != null)
                    this.memoryMappedFile.Dispose();

                this.memoryMappedFile = null;
                this.fullSizeBuffer = null;

                this.lastSuccessVersionBegin = 0;
                this.lastSuccessVersionEnd = 0;

                this.ClearStats();
            }

            // Read success statistics.
            int numReadRetriesPreCheck = 0;
            int numReadRetries = 0;
            int numReadRetriesOnCheck = 0;
            int numReadFailures = 0;
            int numStuckFrames = 0;
            int numReadsSucceeded = 0;
            int numSkippedNoChange = 0;
            uint stuckVersionBegin = 0;
            uint stuckVersionEnd = 0;
            uint lastSuccessVersionBegin = 0;
            uint lastSuccessVersionEnd = 0;
            int maxRetries = 0;

            public string GetStats()
            {
                return string.Format("R1: {0}    R2: {1}    R3: {2}    F: {3}    ST: {4}    MR: {5}    SK:{6}    S:{7}", this.numReadRetriesPreCheck, this.numReadRetries, this.numReadRetriesOnCheck, this.numReadFailures, this.numStuckFrames, this.maxRetries, this.numSkippedNoChange, this.numReadsSucceeded);
            }

            public void ClearStats()
            {
                this.numReadRetriesPreCheck = 0;
                this.numReadRetries = 0;
                this.numReadRetriesOnCheck = 0;
                this.numReadFailures = 0;
                this.numStuckFrames = 0;
                this.numReadsSucceeded = 0;
                this.numSkippedNoChange = 0;
                this.stuckVersionBegin = 0;
                this.stuckVersionEnd = 0;
                this.lastSuccessVersionBegin = 0;
                this.lastSuccessVersionEnd = 0;
                this.maxRetries = 0;
            }

            public void GetMappedDataUnsynchronized(ref MappedBufferT mappedData)
            {
                using (var sharedMemoryStreamView = this.memoryMappedFile.CreateViewStream(0, this.BUFFER_SIZE_BYTES, MemoryMappedFileAccess.Read))
                {
                    var sharedMemoryStream = new BinaryReader(sharedMemoryStreamView);
                    var sharedMemoryReadBuffer = sharedMemoryStream.ReadBytes(this.BUFFER_SIZE_BYTES);

                    var handleBuffer = GCHandle.Alloc(sharedMemoryReadBuffer, GCHandleType.Pinned);
                    mappedData = (MappedBufferT)Marshal.PtrToStructure(handleBuffer.AddrOfPinnedObject(), typeof(MappedBufferT));
                    handleBuffer.Free();
                }
            }

            private void GetHeaderBlock<HeaderBlockT>(BinaryReader sharedMemoryStream, int headerBlockBytes, ref HeaderBlockT headerBlock)
            {
                sharedMemoryStream.BaseStream.Position = 0;
                var sharedMemoryReadBufferHeader = sharedMemoryStream.ReadBytes(headerBlockBytes);

                var handleBufferHeader = GCHandle.Alloc(sharedMemoryReadBufferHeader, GCHandleType.Pinned);
                headerBlock = (HeaderBlockT)Marshal.PtrToStructure(handleBufferHeader.AddrOfPinnedObject(), typeof(HeaderBlockT));
                handleBufferHeader.Free();
            }

            public void GetMappedData(ref MappedBufferT mappedData)
            {
                // This method tries to ensure we read consistent buffer view in three steps.
                // 1. Pre-Check:
                //       - read version header and retry reading this buffer if begin/end versions don't match.  This reduces a chance of
                //         reading torn frame during full buffer read.  This saves CPU time.
                //       - return if version matches last failed read version (stuck frame).
                //       - return if version matches previously successfully read buffer.  This saves CPU time by avoiding the full read of most likely identical data.
                //
                // 2. Main Read: reads the main buffer + version block.  If versions don't match, retry.
                //
                // 3. Post-Check: read version header again and retry reading this buffer if begin/end versions don't match.  This covers corner case
                //                where buffer is being written to during the Main Read.
                //
                // While retrying, this method tries to avoid running CPU at 100%.
                //
                // There are multiple alternatives on what to do here:
                // * keep retrying - drawback is CPU being kept busy, but absolute minimum latency.
                // * Thread.Sleep(0)/Yield - drawback is CPU being kept busy, but almost minimum latency.  Compared to first option, gives other threads a chance to execute.
                // * Thread.Sleep(N) - relaxed approach, less CPU saturation but adds a bit of latency.
                // there are other options too.  Bearing in mind that minimum sleep on windows is ~16ms, which is around 66FPS, I doubt delay added matters much for Crew Chief at least.
                using (var sharedMemoryStreamView = this.memoryMappedFile.CreateViewStream(0, this.BUFFER_SIZE_BYTES, MemoryMappedFileAccess.Read))
                {
                    uint currVersionBegin = 0;
                    uint currVersionEnd = 0;

                    var retry = 0;
                    var sharedMemoryStream = new BinaryReader(sharedMemoryStreamView);
                    byte[] sharedMemoryReadBuffer = null;
                    var versionHeaderWithSize = new RBRMappedBufferVersionBlockWithSize();
                    var versionHeader = new RBRMappedBufferVersionBlock();

                    for (retry = 0; retry < MappedBuffer<MappedBufferT>.NUM_MAX_RETRIEES; ++retry)
                    {
                        var bufferSizeBytes = this.BUFFER_SIZE_BYTES;
                        // Read current buffer versions.
                        if (this.partial)
                        {
                            this.GetHeaderBlock<RBRMappedBufferVersionBlockWithSize>(sharedMemoryStream, this.RBR_BUFFER_VERSION_BLOCK_WITH_SIZE_SIZE_BYTES, ref versionHeaderWithSize);
                            currVersionBegin = versionHeaderWithSize.mVersionUpdateBegin;
                            currVersionEnd = versionHeaderWithSize.mVersionUpdateEnd;

                            bufferSizeBytes = versionHeaderWithSize.mBytesUpdatedHint != 0 ? versionHeaderWithSize.mBytesUpdatedHint : bufferSizeBytes;
                        }
                        else
                        {
                            this.GetHeaderBlock<RBRMappedBufferVersionBlock>(sharedMemoryStream, this.RBR_BUFFER_VERSION_BLOCK_SIZE_BYTES, ref versionHeader);
                            currVersionBegin = versionHeader.mVersionUpdateBegin;
                            currVersionEnd = versionHeader.mVersionUpdateEnd;
                        }

                        // If this is stale "out of sync" situation, that is, we're stuck in, no point in retrying here.
                        // Could be a bug in a game, plugin or a game crash.
                        if (currVersionBegin == this.stuckVersionBegin
                          && currVersionEnd == this.stuckVersionEnd)
                        {
                            ++this.numStuckFrames;
                            return;  // Failed.
                        }

                        // If version is the same as previously successfully read, do nothing.
                        if (this.skipUnchanged
                          && currVersionBegin == this.lastSuccessVersionBegin
                          && currVersionEnd == this.lastSuccessVersionEnd)
                        {
                            ++this.numSkippedNoChange;
                            return;
                        }

                        // Buffer version pre-check.  Verify if Begin/End versions match.
                        if (currVersionBegin != currVersionEnd)
                        {
                            Thread.Sleep(1);
                            ++numReadRetriesPreCheck;
                            continue;
                        }

                        // Read the mapped data.
                        sharedMemoryStream.BaseStream.Position = 0;
                        sharedMemoryReadBuffer = sharedMemoryStream.ReadBytes(bufferSizeBytes);

                        // Marshal version block.
                        var handleVersionBlock = GCHandle.Alloc(sharedMemoryReadBuffer, GCHandleType.Pinned);
                        versionHeader = (RBRMappedBufferVersionBlock)Marshal.PtrToStructure(handleVersionBlock.AddrOfPinnedObject(), typeof(RBRMappedBufferVersionBlock));
                        handleVersionBlock.Free();

                        currVersionBegin = versionHeader.mVersionUpdateBegin;
                        currVersionEnd = versionHeader.mVersionUpdateEnd;

                        // Verify if Begin/End versions match:
                        if (versionHeader.mVersionUpdateBegin != versionHeader.mVersionUpdateEnd)
                        {
                            Thread.Sleep(1);
                            ++numReadRetries;
                            continue;
                        }

                        // Read the version header one last time.  This is for the case, that might not be even possible in reality,
                        // but it is possible in my head.  Since it is cheap, no harm reading again really, aside from retry that
                        // sometimes will be required if buffer is updated between checks.
                        //
                        // Anyway, the case is
                        // * Reader thread reads updateBegin version and continues to read buffer.
                        // * Simultaneously, Writer thread begins overwriting the buffer.
                        // * If Reader thread reads updateEnd before Writer thread finishes, it will look
                        //   like updateBegin == updateEnd.But we actually just read a partially overwritten buffer.
                        //
                        // Hence, this second check is needed here.  Even if writer thread still hasn't finished writing,
                        // we still will be able to detect this case because now updateBegin version changed, so we
                        // know Writer is updating the buffer.

                        this.GetHeaderBlock<RBRMappedBufferVersionBlock>(sharedMemoryStream, this.RBR_BUFFER_VERSION_BLOCK_SIZE_BYTES, ref versionHeader);

                        if (currVersionBegin != versionHeader.mVersionUpdateBegin
                          || currVersionEnd != versionHeader.mVersionUpdateEnd)
                        {
                            Thread.Sleep(1);
                            ++this.numReadRetriesOnCheck;
                            continue;
                        }

                        // Marshal RBR State buffer
                        this.MarshalDataBuffer(this.partial, sharedMemoryReadBuffer, ref mappedData);

                        // Success.
                        this.maxRetries = Math.Max(this.maxRetries, retry);
                        ++this.numReadsSucceeded;
                        this.stuckVersionBegin = this.stuckVersionEnd = 0;

                        // Save succeessfully read version to avoid re-reading.
                        this.lastSuccessVersionBegin = currVersionBegin;
                        this.lastSuccessVersionEnd = currVersionEnd;

                        return;
                    }

                    // Failure.  Save the frame version.
                    this.stuckVersionBegin = currVersionBegin;
                    this.stuckVersionEnd = currVersionEnd;

                    this.maxRetries = Math.Max(this.maxRetries, retry);
                    ++this.numReadFailures;
                }
            }

            private void MarshalDataBuffer(bool partial, byte[] sharedMemoryReadBuffer, ref MappedBufferT mappedData)
            {
                if (partial)
                {
                    // For marshalling to succeed we need to copy partial buffer into full size buffer.  While it is a bit of a waste, it still gives us gain
                    // of shorter time window for version collisions while reading game data.
                    Array.Copy(sharedMemoryReadBuffer, this.fullSizeBuffer, sharedMemoryReadBuffer.Length);
                    var handlePartialBuffer = GCHandle.Alloc(this.fullSizeBuffer, GCHandleType.Pinned);
                    mappedData = (MappedBufferT)Marshal.PtrToStructure(handlePartialBuffer.AddrOfPinnedObject(), typeof(MappedBufferT));
                    handlePartialBuffer.Free();
                }
                else
                {
                    var handleBuffer = GCHandle.Alloc(sharedMemoryReadBuffer, GCHandleType.Pinned);
                    mappedData = (MappedBufferT)Marshal.PtrToStructure(handleBuffer.AddrOfPinnedObject(), typeof(MappedBufferT));
                    handleBuffer.Free();
                }
            }
        }

        MappedBuffer<RBRPerFrameData> perFrameBuffer = new MappedBuffer<RBRPerFrameData>(Constants.MM_MAPPED_PER_FRAME_DATA_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<RBRCoDriverData> coDriverBuffer = new MappedBuffer<RBRCoDriverData>(Constants.MM_MAPPED_CODRIVER_DATA_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);
        MappedBuffer<RBRExtended> extendedBuffer = new MappedBuffer<RBRExtended>(Constants.MM_MAPPED_EXTENDED_FILE_NAME, false /*partial*/, true /*skipUnchanged*/);

        private bool initialised = false;
        private List<RBRStructWrapper> dataToDump;
        private RBRStructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private string lastReadFileName = null;

        private double lastCountdownTime = -1.0;
        private int lastGameMode = -1;

        public class RBRStructWrapper
        {
            public long ticksWhenRead;
            public RBRPerFrameData perFrame;
            public RBRCoDriverData coDriver;
            public RBRExtended extended;
        }

        public override void DumpRawGameData()
        {
            if (this.dumpToFile && this.dataToDump != null && this.dataToDump.Count > 0 && this.filenameToDump != null)
                this.SerializeObject(this.dataToDump.ToArray<RBRStructWrapper>(), this.filenameToDump);
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
                dataReadFromFile = DeSerializeObject<RBRStructWrapper[]>(filePathResolved);

                this.lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }
            if (dataReadFromFile != null && dataReadFromFile.Length > this.dataReadFromFileIndex)
            {
                RBRStructWrapper structWrapperData = dataReadFromFile[this.dataReadFromFileIndex];
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
            this.lastCountdownTime = -1.0;
            this.lastGameMode = -1;

            // This needs to be synchronized, because disconnection happens from CrewChief.Run and MainWindow.Dispose.
            lock (this)
            {
                if (!this.initialised)
                {
                    try
                    {
                        this.perFrameBuffer.Connect();
                        this.coDriverBuffer.Connect();
                        this.extendedBuffer.Connect();

                        // Clear mapped views.
                        this.perFrame = new RBRPerFrameData();
                        this.coDriver = new RBRCoDriverData();
                        this.extended = new RBRExtended();

                        if (dumpToFile)
                            this.dataToDump = new List<RBRStructWrapper>();

                        this.initialised = true;

                        Console.WriteLine("Initialized RBR Shared Memory");
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
        private RBRPerFrameData perFrame;
        private RBRCoDriverData coDriver;
        private RBRExtended extended;

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
                    coDriverBuffer.GetMappedData(ref this.coDriver);
                    perFrameBuffer.GetMappedData(ref this.perFrame);

                    // Create a new copy marshalled views.  Thia is necessary because core code caches states, so each
                    // state has to be an individual object.  We can't avoid copy by marshalling directly into wrapper,
                    // because not all marshalling calls fetch new buffer.
                    var wrapper = new RBRStructWrapper()
                    {
                        extended = this.extended,
                        perFrame = this.perFrame,
                        coDriver = this.coDriver,
                        ticksWhenRead = DateTime.UtcNow.Ticks
                    };

                    if (!forSpotter && currentlyTracingGameData && this.dataToDump != null)
                    {
                        // Exclude empty frames.
                        var modeCurr = wrapper.perFrame.mRBRGameMode.gameMode;
                        var hasGMChanged = modeCurr != this.lastGameMode;
                        this.lastGameMode = modeCurr;

                        var sscCurr = wrapper.perFrame.mRBRCarInfo.stageStartCountdown;
                        var hasSCCChanged = sscCurr != this.lastCountdownTime;
                        this.lastCountdownTime = sscCurr;

                        if (hasGMChanged
                            || hasSCCChanged)
                        {
                            if (wrapper.perFrame.mRBRCarInfo.stageStartCountdown < 6.9f)
                                wrapper.coDriver.mPacenotes = this.GetPopulatedInfoArray<RBRPacenote>(wrapper.coDriver.mPacenotes, wrapper.coDriver.mRBRPacenoteInfo.numPacenotes);
                            else
                                wrapper.coDriver.mPacenotes = this.GetPopulatedInfoArray<RBRPacenote>(wrapper.coDriver.mPacenotes, 0);

                            this.dataToDump.Add(wrapper);
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
                    Console.WriteLine("RBR Shared Memory connection failed.");
                    this.DisconnectInternal();
                    throw new GameDataReadException(ex.Message, ex);
                }
            }
        }

        private InfoT[] GetPopulatedInfoArray<InfoT>(InfoT[] infos, int numPopulated)
        {
            // To reduce serialized size, only return non-empty infos.
            var populated = new List<InfoT>();
            for (int i = 0; i < numPopulated; ++i)
                populated.Add(infos[i]);

            return populated.ToArray();
        }

        public override void DisconnectFromProcess()
        {
            var wasInitialised = this.initialised;
            if (wasInitialised)
            {
                Console.WriteLine("PerFrame: " + this.perFrameBuffer.GetStats());
                Console.WriteLine("Co-Driver: " + this.coDriverBuffer.GetStats());
                Console.WriteLine("Extended: " + this.extendedBuffer.GetStats());
            }

            this.DisconnectInternal();

            // There's still possibility of double message, but who cares.
            if (wasInitialised)
                Console.WriteLine("Disconnected from RBR Shared Memory");

            // Hack to re-check plugin version and reload cars.
            RBRGameStateMapper.pluginVerified = false;
        }

        private void DisconnectInternal()
        {
            this.initialised = false;

            this.perFrameBuffer.Disconnect();
            this.coDriverBuffer.Disconnect();
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
