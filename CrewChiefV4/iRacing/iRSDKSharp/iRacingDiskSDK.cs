using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrewChiefV4;
using CrewChiefV4.iRacing;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

namespace iRSDKSharp
{
    public class iRacingDiskSDK
    {
        public bool IsInitialized = false;
        BinaryReader binaryReader = null;
        MemoryStream memoryStream = null;
        public iRSDKHeader Header;
        public Dictionary<string, VarHeader> VarHeaders = new Dictionary<string, VarHeader>();
        readonly int sizeOfVarHeader = System.Runtime.InteropServices.Marshal.SizeOf(typeof(VarHeader));
        private const int varBufOffset = 48;
        public Dictionary<int, long> lapOffsets = new Dictionary<int, long>();
        private List<string> processedFileNames = new List<string>();
        public AutoResetEvent iRacingDiskDataReady = new AutoResetEvent(false);
        public string sessionInfoString = "";
        public bool hasNewLapData = false;
        public int SessionId { get; set; }
        public int SubsessionId { get; set; }
        private readonly bool deleteTelemetryFile = UserSettings.GetUserSettings().getBoolean("delete_iracing_telemetryfile");
        public void ReadFileData(int sessionId, int SubsessionId)
        {
            lapOffsets.Clear();
            this.SessionId = sessionId;
            this.SubsessionId = SubsessionId;
            ThreadStart ts = GetDiskTelemetryUpdateThread;
            var diskTelemetryUpdateThread = new Thread(ts);
            diskTelemetryUpdateThread.Name = "iRacingDiskSDK.GetDiskTelemetryUpdateThread";
            ThreadManager.RegisterResourceThread(diskTelemetryUpdateThread);
            diskTelemetryUpdateThread.Start();
            IsInitialized = true;
        }

        public T ByteToType<T>() where T : struct
        {
            byte[] bytes = binaryReader.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return theStructure;
        }

        private void ReadVariableHeaders()
        {
            VarHeaders.Clear();
            int offset = Header.varHeaderOffset;
            binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (var i = 0; i < Header.numVars; i++)
            {
                var varHeader = ByteToType<VarHeader>();
                offset += sizeOfVarHeader;
                VarHeaders[varHeader.name] = varHeader;
                binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin);

                //Console.WriteLine($"{(CVarHeader.VarType)varHeader.type}  {varHeader.name}");
                //Console.WriteLine($"Variable description {varHeader.desc}");
            }
        }

        private void GetDiskTelemetryUpdateThread()
        {
            hasNewLapData = false;
            this.memoryStream = new MemoryStream();
            String path = DataFiles.iRacingTelemetryFolder;
            var directory = new DirectoryInfo(path);
            var file = directory.GetFiles("*.ibt").OrderByDescending(f => f.LastWriteTime).First();
            FileStream telemetryFileStream = null;
            try
            {
                telemetryFileStream = File.Open(file.FullName, FileMode.Open);
                processedFileNames.Add(file.FullName);
            }
            catch (Exception ex)
            {
                iRacingDiskDataReady.Set();
                return;
            }
            try
            {
                if (memoryStream != null)
                {
                    telemetryFileStream.CopyTo(memoryStream);
                    telemetryFileStream?.Dispose();
                    this.binaryReader = new BinaryReader(memoryStream);
                    this.binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    this.Header = ByteToType<iRSDKHeader>();
                    Console.WriteLine("Reading iRacing telemetryfile:" + file);
                    ReadVariableHeaders();
                    GetOffsetsForLaps();
                    hasNewLapData = true;
                    if(deleteTelemetryFile)
                    {
                        File.Delete(file.FullName);
                    }
                }
            }
            catch (Exception ex) { Log.Exception(ex); }
            iRacingDiskDataReady.Set();
        }
        public List<object> GetDataForLap(string name, int lapNumber)
        {
            List<object> dataOut = new List<object>();
            if (VarHeaders.TryGetValue(name, out VarHeader varHeader))
            {
                if (lapOffsets.TryGetValue(lapNumber, out long offset))
                {
                    long nextLapOffset = -1;
                    lapOffsets.TryGetValue(lapNumber + 1, out nextLapOffset);

                    offset += varHeader.offset;
                    int count = varHeader.count;
                    while ((offset <= binaryReader.BaseStream.Length && nextLapOffset == -1) || offset < nextLapOffset)
                    {
                        if (binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin) == offset)
                        {
                            if (varHeader.type == (int)CVarHeader.VarType.irChar)
                            {
                                byte[] data = binaryReader.ReadBytes(count);
                                dataOut.Add(System.Text.Encoding.Default.GetString(data).TrimEnd(new char[] { '\0' }));
                            }
                            else if (varHeader.type == (int)CVarHeader.VarType.irBool)
                            {
                                if (count > 1)
                                {
                                    bool[] data = new bool[count];
                                    for (int i = 0; i < count; i++)
                                    {
                                        data[i] = binaryReader.ReadBoolean();
                                    }
                                    dataOut.Add(data[0]);
                                }
                                else
                                {
                                    dataOut.Add(binaryReader.ReadBoolean());
                                }
                            }
                            if (varHeader.type == (int)CVarHeader.VarType.irInt)
                            {
                                if (count > 1)
                                {
                                    int[] data = new int[count];
                                    for (int i = 0; i < count; i++)
                                    {
                                        data[i] = binaryReader.ReadInt32();
                                    }
                                    dataOut.Add(data.Average());
                                }
                                else
                                {
                                    dataOut.Add(binaryReader.ReadInt32());
                                }
                            }
                            else if (varHeader.type == (int)CVarHeader.VarType.irFloat)
                            {
                                if (count > 1)
                                {
                                    float[] data = new float[count];
                                    for (int i = 0; i < count; i++)
                                    {
                                        data[i] = binaryReader.ReadSingle();
                                    }
                                    dataOut.Add(data.Average());
                                }
                                else
                                {
                                    dataOut.Add(binaryReader.ReadSingle());
                                }
                            }
                            else if (varHeader.type == (int)CVarHeader.VarType.irDouble)
                            {
                                if (count > 1)
                                {
                                    double[] data = new double[count];
                                    for (int i = 0; i < count; i++)
                                    {
                                        data[i] = binaryReader.ReadDouble();
                                    }
                                    dataOut.Add(data.Average());
                                }
                                else
                                {
                                    dataOut.Add(binaryReader.ReadDouble());
                                }
                            }
                        }
                        offset += Header.bufLen;
                    }
                }
            }
            return dataOut;
        }

        private void GetOffsetsForLaps()
        {
            binaryReader.BaseStream.Seek(varBufOffset, SeekOrigin.Begin);
            VarBuf varBuf = ByteToType<VarBuf>();
            if (VarHeaders.TryGetValue("Lap", out VarHeader varHeader))
            {
                int offset = varBuf.bufOffset + varHeader.offset;
                int previousLapNumber = -1;
                while (offset <= binaryReader.BaseStream.Length)
                {
                    if (binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin) == offset)
                    {
                        int lapNumber = binaryReader.ReadInt32();
                        if(lapNumber > previousLapNumber)
                        {
                            previousLapNumber = lapNumber;
                            lapOffsets[lapNumber] = offset - varHeader.offset;
                        }
                    }
                    offset += Header.bufLen;
                }
            }
            return;
        }

        public object GetData(string name, int lapNumber)
        {
            binaryReader.BaseStream.Seek(varBufOffset, SeekOrigin.Begin);
            VarBuf varBuf = ByteToType<VarBuf>();
            if (VarHeaders.TryGetValue(name, out VarHeader varHeader))
            {
                if (lapOffsets.TryGetValue(lapNumber, out long offset))
                {
                    offset += varHeader.offset;
                    int count = varHeader.count;
                    if (binaryReader.BaseStream.Seek(offset, SeekOrigin.Begin) == offset)
                    {
                        if (varHeader.type == (int)CVarHeader.VarType.irChar)
                        {
                            byte[] data = binaryReader.ReadBytes(count);
                            return System.Text.Encoding.Default.GetString(data).TrimEnd(new char[] { '\0' });
                        }
                        else if (varHeader.type == (int)CVarHeader.VarType.irBool)
                        {
                            if (count > 1)
                            {
                                bool[] data = new bool[count];
                                for (int i = 0; i < count; i++)
                                {
                                    data[i] = binaryReader.ReadBoolean();
                                }
                                return data;
                            }
                            else
                            {
                                return binaryReader.ReadBoolean();
                            }
                        }
                        if (varHeader.type == (int)CVarHeader.VarType.irInt)
                        {
                            if (count > 1)
                            {
                                int[] data = new int[count];
                                for (int i = 0; i < count; i++)
                                {
                                    data[i] = binaryReader.ReadInt32();
                                }
                                return data;
                            }
                            else
                            {
                                return binaryReader.ReadInt32();
                            }
                        }
                        else if (varHeader.type == (int)CVarHeader.VarType.irFloat)
                        {
                            if (count > 1)
                            {
                                float[] data = new float[count];
                                for (int i = 0; i < count; i++)
                                {
                                    data[i] = binaryReader.ReadSingle();
                                }
                                return data;
                            }
                            else
                            {
                                return binaryReader.ReadSingle();
                            }
                        }
                        else if (varHeader.type == (int)CVarHeader.VarType.irDouble)
                        {
                            if (count > 1)
                            {
                                double[] data = new double[count];
                                for (int i = 0; i < count; i++)
                                {
                                    data[i] = binaryReader.ReadDouble();
                                }
                                return data;
                            }
                            else
                            {
                                return binaryReader.ReadDouble();
                            }
                        }
                    }
                }
            }
            return null;
        }
        public string GetSessionInfoString()
        {
            binaryReader.BaseStream.Seek(Header.sessionInfoOffset,SeekOrigin.Begin);
            byte[] data = binaryReader.ReadBytes(Header.sessionInfoLen);
            return System.Text.Encoding.Default.GetString(data).TrimEnd(new char[] { '\0' });
        }
        public void ClearData()
        {
            binaryReader?.Dispose();
            memoryStream?.Dispose();
            IsInitialized = false;
        }
    }


}
