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
                    int retries = -1;
                    do {
                        retries++;
                        using (var sharedMemoryStreamView = memoryMappedFile.CreateViewStream(0, sharedmemorysize, MemoryMappedFileAccess.Read))
                        {
                            BinaryReader _SharedMemoryStream = new BinaryReader(sharedMemoryStreamView);
                            sharedMemoryReadBuffer = _SharedMemoryStream.ReadBytes(sharedmemorysize);
                            GCHandle handle = GCHandle.Alloc(sharedMemoryReadBuffer, GCHandleType.Pinned);
                            try
                            {
                                _ams2apistruct = (ams2APIStruct)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ams2APIStruct));
                            }
                            finally
                            {
                                handle.Free();
                            }
                        }
                    } while (_ams2apistruct.mSequenceNumber % 2 != 0);
                    tornFramesCount += retries;
                    AMS2StructWrapper structWrapper = new AMS2StructWrapper();
                    structWrapper.ticksWhenRead = DateTime.UtcNow.Ticks;
                    structWrapper.data = _ams2apistruct;
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
