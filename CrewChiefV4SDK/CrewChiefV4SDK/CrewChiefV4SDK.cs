using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrewChiefV4SharedMemory
{
    public enum VarType { ccChar, ccBool, ccWChar, ccInt, ccFloat, ccDouble, ccInt64, stringArray, wstringArray };

    public enum UpdateStatus { disconnected = 0, connected, updating };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct VarHeader
    {
        const int offsetType = 0;
        const int offsetTypeSize = 4;
        const int offsetOffset = 8;
        const int offsetCount = 12;
        const int offsetName = 16;
        const int offsetDesc = 80;
        const int offsetUnit = 336;
        /// <summary>
        /// variable type enum VarType { ccChar, ccBool, ccWChar, ccInt, ccFloat, ccDouble, ccInt64, stringArray, wstringArray }
        /// </summary>
        public int type;
        /// <summary>
        /// Size of the type.
        /// </summary>
        public int typeSize;
        /// <summary>
        /// Relative offset in shared memory where variable is loacted. ( CrewChiefSharedHeader.bufOffset + this.offset)
        /// </summary>
        public int offset;
        /// <summary>
        /// Number of elements in variable. 
        /// </summary>
        public int count;
        /// <summary>
        /// Variable name. (unicode)(size 64 bytes)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string name;
        /// <summary>
        /// Variable description. (unicode)(size 256 bytes) 
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string desc;
        /// <summary>
        /// type of unit. eg. ms, kph, mph ... (unicode)(size 64 bytes) 
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string unit;
    }
    // Main header for shared memory 
    public struct CrewChiefSharedHeader
    {
        const int offsetVer = 0;
        const int offsetNumVars = 4;
        const int offsetVarHeaderOffset = 8;
        const int offsetBufOffset = 12;
        /// <summary>
        /// Header version.
        /// </summary>
        public Int32 ver;
        /// <summary>
        /// Number of variables currently mapped.
        /// </summary>
        public Int32 numVars;
        /// <summary>
        /// Start offset in shared memory where variable headers are written to.  
        /// </summary>
        public Int32 varHeaderOffset;
        /// <summary>
        /// Start offset in shared memory where all variable buffers are written.
        /// </summary>
        public Int32 bufOffset;
    }

    public class CrewChiefV4SDK
    {
        private const String CREWCHIEF_SHARED_MEMORY_REGION = @"Local\CrewChiefV4";
        private static readonly String CREWCHIEF_SHARED_MEMORY_EVENT = @"Local\CrewChiefV4_DataReadyEvent";
        private static readonly int varHeaderSize = Marshal.SizeOf(typeof(VarHeader));
        internal  MemoryMappedFile memoryMappedFile = null;
        internal  Dictionary<string, VarHeader> varHeaders = new Dictionary<string, VarHeader>();
        internal  MemoryMappedViewAccessor mappedView;       
        internal  CrewChiefSharedHeader headerData;
        EventWaitHandle eventWaitHandle = null;
        public bool initialized = false;
        private int lastTick = 0;
        public void initialize()
        {
            bool eventWritten = false;
            Console.WriteLine("waiting for data ready event");
            while (eventWritten = EventWaitHandle.TryOpenExisting(CREWCHIEF_SHARED_MEMORY_EVENT, out eventWaitHandle) == false)
            {
                Thread.Sleep(500);
            }
            eventWaitHandle.WaitOne();
            
            memoryMappedFile = MemoryMappedFile.OpenExisting(CREWCHIEF_SHARED_MEMORY_REGION);
            mappedView = memoryMappedFile.CreateViewAccessor();
            // read static header data
            Console.WriteLine("Connected to CrewChiefV4 shared Memory");
            Console.WriteLine("Reading header Data");
            mappedView.Read(0, out headerData);

            for (int i = 0; i < headerData.numVars; i++)
            {
                using (MemoryMappedViewStream mmvs = memoryMappedFile.CreateViewStream())
                {
                    BinaryReader _SharedMemoryStream = new BinaryReader(mmvs);
                    _SharedMemoryStream.BaseStream.Seek(headerData.varHeaderOffset + (varHeaderSize * i), SeekOrigin.Begin);
                    byte[] varHeader = _SharedMemoryStream.ReadBytes(varHeaderSize);
                    VarHeader header = Deserialize<VarHeader>(varHeader);
                    varHeaders[header.name] = header;
                }
            }
            Console.WriteLine("Done reading header data");
            Console.WriteLine("Waiting for CrewChiefV4 shared memory to start updating");
            initialized = true;
        }
        public bool IsUpdating()
        {
            int? updateStatus = (int?)GetData("updateStatus");
            return updateStatus.HasValue && (UpdateStatus)updateStatus.Value == UpdateStatus.updating;
        }
        public void Tick()
        {
            int? tickCount = (int?)GetData("tickCount");
            int? tickRate = (int?)GetData("tickRate");
            while (tickCount.HasValue && tickCount.Value == lastTick)
            {
                lastTick = tickCount.Value;
                Thread.Sleep(tickRate.Value);
            }
        }
        public bool IsConnected()
        {
            int? updateStatus = (int?)GetData("updateStatus");
            if(!updateStatus.HasValue)
            {
                return false;
            }
            return updateStatus.HasValue && (UpdateStatus)updateStatus.Value != UpdateStatus.disconnected;
        }

        internal T Deserialize<T>(byte[] array) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            var s = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return s;
        }
        

        // Generates a C# Class that  contains all current variables.
        public void GenerateCSharpDataClass()
        {
            String dataFilesPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), @"..\", @"..\CrewChiefData.cs");
            Console.WriteLine("Writing CrewChiefData.cs to: " + dataFilesPath);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(dataFilesPath))
            {
                file.WriteLine("using System;");
                file.WriteLine("namespace CrewChiefV4SharedMemory");
                file.WriteLine("{");
                file.WriteLine("\t[Serializable]");
                file.WriteLine("\tpublic class CrewChiefData");
                file.WriteLine("\t{");
                file.WriteLine("\t\tpublic CrewChiefData(CrewChiefV4SDK sdk)");
                file.WriteLine("\t\t{");
                foreach (var header in varHeaders)
                {
                    file.WriteLine("\t\t\t" + header.Value.name + " = " + "(" + GetData(header.Value.name).GetType().ToString() + ")" + "sdk.GetData(\"" + header.Value.name + "\");");
                }
                file.WriteLine("\t\t}");
                foreach (var header in varHeaders)
                {
                    file.WriteLine("");
                    file.WriteLine("\t\t/// <summary>");
                    file.WriteLine("\t\t/// " + header.Value.desc);
                    file.WriteLine("\t\t/// <summary>");
                    file.WriteLine("\t\tpublic " + GetData(header.Value.name).GetType().ToString() + " " + header.Value.name + ";");
                }
                file.WriteLine("\t}");
                file.WriteLine("}");
            }
        }
        public object GetData(string name)
        {
            if (varHeaders.TryGetValue(name, out VarHeader header))
            {
                if (header.count == 1)
                {
                    switch ((VarType)header.type)
                    {
                        case VarType.ccChar:
                            return mappedView.ReadChar(header.offset + headerData.bufOffset);
                        case VarType.ccBool:
                            return mappedView.ReadBoolean(header.offset + headerData.bufOffset);
                        case VarType.ccWChar:
                            return mappedView.ReadInt16(header.offset + headerData.bufOffset);
                        case VarType.ccInt:
                            return mappedView.ReadInt32(header.offset + headerData.bufOffset);
                        case VarType.ccFloat:
                            return mappedView.ReadSingle(header.offset + headerData.bufOffset);
                        case VarType.ccDouble:
                            return mappedView.ReadDouble(header.offset + headerData.bufOffset);
                        case VarType.ccInt64:
                            return mappedView.ReadInt64(header.offset + headerData.bufOffset);
                        default:
                            return null;
                    }
                }
                else
                {
                    switch ((VarType)header.type)
                    {
                        case VarType.ccChar:
                            {
                                byte[] data = new byte[header.count];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, header.count);
                                return Encoding.Unicode.GetString(data).TrimEnd(new char[] { '\0' }); ;
                            }
                        case VarType.ccBool:
                            {
                                bool[] data = new bool[header.count];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, header.count);
                                return data;
                            }
                        case VarType.ccWChar:
                            {
                                byte[] data = new byte[header.count * 2];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, data.Length);
                                return Encoding.Unicode.GetString(data).TrimEnd(new char[] { '\0' });
                            }
                        case VarType.ccInt:
                            {
                                int[] data = new int[header.count];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, data.Length);
                                return data;
                            }

                        case VarType.ccFloat:
                            {
                                float[] data = new float[header.count];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, data.Length);
                                return data;
                            }
                        case VarType.ccDouble:
                            {
                                double[] data = new double[header.count];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, data.Length);
                                return data;
                            }
                        case VarType.ccInt64:
                            {
                                Int64[] data = new Int64[header.count];
                                mappedView.ReadArray(header.offset + headerData.bufOffset, data, 0, data.Length);
                                return data;
                            }
                        case VarType.wstringArray:
                            {
                                string[] stringArray = new string[header.count];
                                int position = header.offset + headerData.bufOffset;
                                for (int i = 0; i < header.count; i++)
                                {
                                    byte[] data = new byte[header.typeSize];
                                    mappedView.ReadArray(position, data, 0, data.Length);
                                    stringArray[i] = Encoding.Unicode.GetString(data).TrimEnd(new char[] { '\0' });
                                    position += header.typeSize;
                                }
                                return stringArray;
                            }
                        case VarType.stringArray:
                            {
                                string[] stringArray = new string[header.count];
                                int position = header.offset + headerData.bufOffset;
                                for (int i = 0; i < header.count; i++)
                                {
                                    byte[] data = new byte[header.typeSize];
                                    mappedView.ReadArray(position, data, 0, data.Length);
                                    stringArray[i] = Encoding.ASCII.GetString(data).TrimEnd(new char[] { '\0' });
                                    position += header.typeSize;
                                }
                                return stringArray;
                            }
                        default:
                            return null;
                    }
                }
            }
            return null;
        }
    }
}
