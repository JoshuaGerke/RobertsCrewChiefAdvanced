using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrewChiefV4.AMS2
{
    public class AMS2SharedMemoryReader : GameDataReader
    {
        private MemoryMappedFile memoryMappedFile;
        private MemoryMappedViewAccessor sharedMemoryView;
        private uint lastSequence;
        private long lastSourceTicks;
        public const int SequenceOffset=7320;
        public const int MaximumSnapshotAttempts=8;
        private int sharedmemorysize;
        private byte[] sharedMemoryReadBuffer;
        private Boolean initialised = false;
        private List<AMS2StructWrapper> dataToDump;
        private AMS2StructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private String lastReadFileName = null;
        private long tornFramesCount = 0;
        
        public class AMS2StructWrapper
        {
            public long ticksWhenRead;
            public long sourceSampleTicks;
            public bool sdk14LayoutSelected;
            public ams2APIStruct data;
        }

        public override void DumpRawGameData()
        {
            if (dumpToFile && dataToDump != null && dataToDump.Count > 0 && filenameToDump != null)
            {
                SerializeObject(dataToDump.ToArray<AMS2StructWrapper>(), filenameToDump);
            }
        }

        public override void ResetGameDataFromFile()
        {
            dataReadFromFileIndex = 0;
        }

        public override TracedGameData ReadGameDataFromFile(String filename, int pauseBeforeStart)
        {
            if (dataReadFromFile == null || filename != lastReadFileName)
            {
                dataReadFromFileIndex = 0;
                var filePathResolved = Utilities.ResolveDataFile(this.dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<AMS2StructWrapper[]>(filePathResolved);

                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }
            if (dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex)
            {
                AMS2StructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex];
                dataReadFromFileIndex++;
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

        public ams2APIStruct BytesToStructure(byte[] bytes)
        {
            if(bytes==null || bytes.Length!=Marshal.SizeOf(typeof(ams2APIStruct)))
                throw new ArgumentException("AMS2 snapshot length does not match the supported SDK layout.");
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, ptr, bytes.Length);
                return (ams2APIStruct)Marshal.PtrToStructure(ptr, typeof(ams2APIStruct));
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        public static bool TryReadStableFrame(Func<uint> sequence,Action<byte[]> copy,byte[] buffer,out int retries)
        {
            if(buffer==null || buffer.Length<SequenceOffset+4)throw new ArgumentException("Snapshot buffer too short.");
            for(retries=0;retries<MaximumSnapshotAttempts;retries++)
            {
                uint before=sequence();
                if((before&1)!=0)continue;
                copy(buffer);
                uint after=sequence();
                if(before==after && (after&1)==0 && BitConverter.ToUInt32(buffer,SequenceOffset)==before)return true;
            }
            return false;
        }

        protected override Boolean InitialiseInternal()
        {
            if (dumpToFile)
            {
                dataToDump = new List<AMS2StructWrapper>();
            }
            lock (this)
            {
                if (!initialised)
                {
                    try
                    {
                        memoryMappedFile = MemoryMappedFile.OpenExisting("$pcars2$", MemoryMappedFileRights.Read);
                        sharedmemorysize = Marshal.SizeOf(typeof(ams2APIStruct));
                        sharedMemoryReadBuffer = new byte[sharedmemorysize];
                        sharedMemoryView=memoryMappedFile.CreateViewAccessor(0,sharedmemorysize,MemoryMappedFileAccess.Read);
                        lastSourceTicks=0;
                        initialised = true;
                        Console.WriteLine("Initialised ams2 shared memory");
                    }
                    catch (Exception)
                    {
                        initialised = false;
                    }
                }
                return initialised;
            }            
        }

        public override void stop()
        {
            Console.WriteLine("Stopped reading ams2 data, discarded " + tornFramesCount + " torn frames");
        }

        public override Object ReadGameData(Boolean forSpotter)
        {
            lock (this)
            {
                ams2APIStruct _ams2apistruct = new ams2APIStruct();
                if (!initialised)
                {
                    if (!InitialiseInternal())
                    {
                        throw new GameDataReadException("Failed to initialise shared memory");
                    }
                }
                try
                {
                    int retries;
                    bool stable=TryReadStableFrame(()=>sharedMemoryView.ReadUInt32(SequenceOffset),
                        target=>{if(sharedMemoryView.ReadArray(0,target,0,target.Length)!=target.Length)throw new IOException("Incomplete AMS2 snapshot.");},
                        sharedMemoryReadBuffer,out retries);
                    tornFramesCount+=retries;
                    if(!stable)throw new IOException("AMS2 snapshot is being updated; bounded retry exhausted.");
                    _ams2apistruct=BytesToStructure(sharedMemoryReadBuffer);
                    long now=DateTime.UtcNow.Ticks;
                    if(lastSourceTicks==0 || lastSequence!=_ams2apistruct.mSequenceNumber)
                    {lastSequence=_ams2apistruct.mSequenceNumber;lastSourceTicks=now;}
                    AMS2StructWrapper structWrapper = new AMS2StructWrapper();
                    structWrapper.ticksWhenRead = DateTime.UtcNow.Ticks;
                    structWrapper.data = _ams2apistruct;
                    structWrapper.sourceSampleTicks=lastSourceTicks;
                    structWrapper.sdk14LayoutSelected=_ams2apistruct.mVersion==14 && sharedmemorysize==20700;
                    if (!forSpotter && currentlyTracingGameData && dataToDump != null && _ams2apistruct.mTrackLocation != null &&
                        _ams2apistruct.mTrackLocation.Length > 0)
                    {
                        dataToDump.Add(structWrapper);
                    }
                    return structWrapper;
                }
                catch (Exception ex)
                {
                    throw new GameDataReadException(ex.Message, ex);
                }
            }            
        }

        public override void Dispose()
        {
            if(sharedMemoryView!=null){sharedMemoryView.Dispose();sharedMemoryView=null;}
            if (memoryMappedFile != null)
            {
                try
                {
                    memoryMappedFile.Dispose();
                    memoryMappedFile = null;
                }
                catch (Exception e) {Log.Exception(e);}
            }
            initialised = false;
        }
    }
}
