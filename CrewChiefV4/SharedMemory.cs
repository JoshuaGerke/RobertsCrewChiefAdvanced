using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrewChiefV4.SharedMemory
{
    // Do we need more data types ?
    public enum VarType { ccChar, ccBool, ccWChar, ccInt, ccFloat, ccDouble, ccInt64, stringArray, wstringArray };

    public enum UpdateStatus { disconnected = 0, connected, updating };

    public enum PhraseVoiceType { chief = 0, spotter, you }
    // fixed size of 400, not reflected!
    // the layout of this class should always stay the same so we need to make sure this is the layout we want going forward.
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
        public string name;
        /// <summary>
        /// Variable description. (unicode)(size 256 bytes) 
        /// </summary>
        public string desc;
        /// <summary>
        /// type of unit. eg. ms, kph, mph ... (unicode)(size 64 bytes) 
        /// </summary>
        public string unit;
        public VarHeader(int type, int typeSize, int offset, int count, string name, string desc, string unit)
        {
            this.type = type;
            this.typeSize = typeSize;
            this.offset = offset;
            this.count = count;
            this.name = name;
            this.desc = desc;
            this.unit = unit;
        }
        public void WriteVarHeader(MemoryMappedViewAccessor FileMapView, int position)
        {
            FileMapView.Write(position, this.type);
            FileMapView.Write(position + offsetTypeSize, this.typeSize);
            FileMapView.Write(position + offsetOffset, this.offset);
            FileMapView.Write(position + offsetCount, this.count);
            byte[] nameBytes = new byte[64];
            Encoding.Unicode.GetBytes(this.name).CopyTo(nameBytes, 0);
            FileMapView.WriteArray(position + offsetName, nameBytes, 0, nameBytes.Length);
            byte[] descBytes = new byte[256];
            Encoding.Unicode.GetBytes(this.desc).CopyTo(descBytes, 0);
            FileMapView.WriteArray(position + offsetDesc, descBytes, 0, descBytes.Length);
            byte[] unitBytes = new byte[64];
            Encoding.Unicode.GetBytes(this.unit).CopyTo(unitBytes, 0);
            FileMapView.WriteArray(position + offsetUnit, unitBytes, 0, unitBytes.Length);
        }
    }
    public struct Phrase
    {
        public Int32 phraseSequenceId;
        public Int64 fileTime;
        // 512
        public string voiceName;
        // 8192 (2x the longest rant).
        public string phrase;

        public int voiceType;

        public Phrase(Int32 phraseSequenceId, string voiceName, string phrase, int voiceType)
        {
            this.phraseSequenceId = phraseSequenceId;
            this.fileTime = DateTime.Now.ToFileTime();
            this.voiceName = voiceName;
            this.phrase = phrase;
            this.voiceType = voiceType;
        }
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

    public class SharedMemoryManager 
    {
        private static readonly String CREWCHIEF_SHARED_MEMORY_REGION = @"Local\CrewChiefV4";
        private static readonly String CREWCHIEF_SHARED_MEMORY_DATA_READY_EVENT = @"Local\CrewChiefV4_DataReadyEvent";
        private EventWaitHandle smUpdateEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CREWCHIEF_SHARED_MEMORY_DATA_READY_EVENT);

        private const int varHeaderSize = 400;
        private static readonly int headerSize = Marshal.SizeOf(typeof(CrewChiefSharedHeader));

        private MemoryMappedFile mappedFileView = null;
        private MemoryMappedViewAccessor FileMapView = null;
        private CrewChiefSharedHeader header;

        private Dictionary<string, VarHeader> varHeaders = null;

        private int tickCount = 0;

        public SharedMemoryManager()
        {
            header = new CrewChiefSharedHeader();

            // Use CreateOrOpen as clients might hold a handle to already existing mapped region
            mappedFileView = MemoryMappedFile.CreateOrOpen(CREWCHIEF_SHARED_MEMORY_REGION, 500000, MemoryMappedFileAccess.ReadWrite);

            FileMapView = mappedFileView.CreateViewAccessor();
            varHeaders = new Dictionary<string, VarHeader>();
            // add variables to shared memory, all dynamic variable needs to be added here in the initializer to calculate offsets.
            // 
            int offsetNextBuf = AddVarHeader((int)VarType.ccInt, 0, 1, "updateStatus", "enum UpdateStatus { disconnected = 0, connected, updating }");
            offsetNextBuf = AddVarHeader((int)VarType.ccInt, offsetNextBuf, 1, "tickRate", "Current tick rate app is pumping updates", "ms");
            offsetNextBuf = AddVarHeader((int)VarType.ccInt, offsetNextBuf, 1, "tickCount", "Tick Counter");
            offsetNextBuf = AddVarHeader((int)VarType.ccInt, offsetNextBuf, 1, "numTotalPhrases", "Total number of phrases[] populated");
            offsetNextBuf = AddVarHeader((int)VarType.ccInt, offsetNextBuf, 1, "lastPhraseIndex", "Last phrases[] index written to");
            offsetNextBuf = AddVarHeader((int)VarType.ccInt, offsetNextBuf, 10, "phraseSequenceIds", "parent phrase id");
            offsetNextBuf = AddVarHeader((int)VarType.ccInt64, offsetNextBuf, 10, "phraseFileTimes", "Last update time" );
            offsetNextBuf = AddVarHeader((int)VarType.wstringArray, offsetNextBuf, 10, "phraseVoiceNames", "Phrase voice name", "" , 256);
            offsetNextBuf = AddVarHeader((int)VarType.wstringArray, offsetNextBuf, 10, "phrasePhrases", "phrases", "", 4096);
            offsetNextBuf = AddVarHeader((int)VarType.ccInt, offsetNextBuf, 10, "phrasesVoiceType", "enum PhraseVoiceType { chief = 0, spotter, you }", "");
            offsetNextBuf = AddVarHeader((int)VarType.ccBool, offsetNextBuf, 1, "phraseIsPlaying", "Is the phrase currently playing");

            header.ver = 1;
            header.varHeaderOffset = headerSize;
            header.numVars = varHeaders.Count;
            header.bufOffset = header.varHeaderOffset + (varHeaderSize * header.numVars);
            UpdateVariable("updateStatus", new int[1] { (int)UpdateStatus.connected });
            UpdateVariable("tickRate", new int[1] { 0 });
            FileMapView.Write(0, ref header);
            smUpdateEvent.Set();
        }
        public void Dispose()
        {
            tickCount++;            
            UpdateVariable("updateStatus", new int[1] { (int)UpdateStatus.disconnected });
            UpdateVariable("tickCount", new int[1] { tickCount });

            FileMapView?.Dispose();
            mappedFileView?.Dispose();
            smUpdateEvent?.Reset();
            smUpdateEvent?.Dispose();

        }
        public void Tick(int tickRate, UpdateStatus updateStatus = UpdateStatus.updating)
        {
            tickCount++;
            UpdateVariable("tickRate", new int[1] { tickRate });
            UpdateVariable("updateStatus", new int[1] { (int)updateStatus });
            UpdateVariable("tickCount", new int[1] { tickCount });
        }
        private int AddVarHeader(int type, int offset, int count, string name, string desc, string unit = "", int charArraySize = 0)
        {
            int position = headerSize + (varHeaders.Count * varHeaderSize);
            int typeSize = GetTypeSize((VarType)type);
            if ((VarType)type == VarType.wstringArray || (VarType)type == VarType.stringArray)
            {
                typeSize = typeSize * charArraySize;
            }
            var curHeader = varHeaders[name] = new VarHeader(type, typeSize, offset, count, name, desc, unit);
            curHeader.WriteVarHeader(FileMapView, position);
            return offset + (curHeader.typeSize * count);
        }
        public void UpdateVariable<T>(string name, T[] values)
        {
            if(values == null)
            {
                return;
            }
            lock (varHeaders)
            {
                if (varHeaders.TryGetValue(name, out var varHeader))
                {
                    lock (FileMapView)
                    {
                        int position = varHeader.offset + header.bufOffset;
                        int valArrayLen = values.Length;
                        if (varHeader.count == 1)
                        {
                            if ((VarType)varHeader.type == VarType.ccBool)
                            {
                                FileMapView.Write(position, Convert.ToBoolean(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.ccChar)
                            {
                                FileMapView.Write(position, Convert.ToChar(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.ccWChar)
                            {
                                FileMapView.Write(position, Convert.ToInt16(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.ccInt)
                            {
                                FileMapView.Write(position, Convert.ToUInt32(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.ccFloat)
                            {
                                FileMapView.Write(position, Convert.ToSingle(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.ccDouble)
                            {
                                FileMapView.Write(position, Convert.ToDouble(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.ccInt64)
                            {
                                FileMapView.Write(position, Convert.ToInt64(values[0]));
                            }
                            if ((VarType)varHeader.type == VarType.stringArray)
                            {
                                byte[] str = new byte[varHeader.typeSize];
                                Encoding.ASCII.GetBytes(values[0].ToString()).CopyTo(str, 0);
                                FileMapView.WriteArray(position, str, 0, str.Length);
                            }
                            if ((VarType)varHeader.type == VarType.wstringArray)
                            {
                                byte[] wstr = new byte[varHeader.typeSize];
                                Encoding.Unicode.GetBytes(values[0].ToString()).CopyTo(wstr, 0);
                                FileMapView.WriteArray(position, wstr, 0, wstr.Length);
                            }
                        }
                        else
                        {

                            if ((VarType)varHeader.type == VarType.ccBool)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    FileMapView.Write(position, Convert.ToChar(values[i]));
                                    position += varHeader.typeSize;
                                }
                            }
                            if ((VarType)varHeader.type == VarType.ccChar)
                            {
                                string encodedString = values[0].ToString();
                                byte[] str = new byte[varHeader.count];
                                Encoding.ASCII.GetBytes(encodedString).CopyTo(str, 0);
                                FileMapView.WriteArray(position, str, 0, str.Length);
                            }
                            if ((VarType)varHeader.type == VarType.ccWChar)
                            {
                                byte[] wstr = new byte[varHeader.count * 2];
                                Encoding.Unicode.GetBytes(values[0].ToString()).CopyTo(wstr, 0);
                                FileMapView.WriteArray(position, wstr, 0, wstr.Length);
                            }
                            if ((VarType)varHeader.type == VarType.ccInt)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    FileMapView.Write(position, Convert.ToUInt32(values[i]));
                                    position += varHeader.typeSize;
                                }
                            }
                            if ((VarType)varHeader.type == VarType.ccFloat)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    FileMapView.Write(position, Convert.ToSingle(values[i]));
                                    position += varHeader.typeSize;
                                }
                            }
                            if ((VarType)varHeader.type == VarType.ccDouble)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    FileMapView.Write(position, Convert.ToDouble(values[i]));
                                    position += varHeader.typeSize;
                                }
                            }
                            if ((VarType)varHeader.type == VarType.ccInt64)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    FileMapView.Write(position, Convert.ToInt64(values[i]));
                                    position += varHeader.typeSize;
                                }
                            }
                            if ((VarType)varHeader.type == VarType.stringArray)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    byte[] str = new byte[varHeader.typeSize];
                                    Encoding.ASCII.GetBytes(values[i].ToString()).CopyTo(str, 0);
                                    FileMapView.WriteArray(position, str, 0, str.Length);
                                    position += varHeader.typeSize;
                                }
                            }

                            if ((VarType)varHeader.type == VarType.wstringArray)
                            {
                                for (int i = 0; i < valArrayLen; i++)
                                {
                                    byte[] wstr = new byte[varHeader.typeSize];
                                    Encoding.Unicode.GetBytes(values[i].ToString()).CopyTo(wstr, 0);
                                    FileMapView.WriteArray(position, wstr, 0, wstr.Length);
                                    position += varHeader.typeSize;
                                }
                            }
                        }
                    }
                }
            }
        
        }
        public int GetTypeSize(VarType varType)
        {
            switch (varType)
            {
                case VarType.ccBool:
                case VarType.ccChar:
                case VarType.stringArray:
                    return 1;
                case VarType.ccWChar:
                case VarType.wstringArray:
                    return 2;
                case VarType.ccFloat:
                case VarType.ccInt:
                    return 4;
                case VarType.ccInt64:
                case VarType.ccDouble:
                    return 8;                
                //case VarType.ccPhrase:
                //    return 12816;
                default:
                    return 4;
            }
        }

        public void WritePhrases(Phrase[] phrases)
        {
            if (varHeaders.TryGetValue("phrasePhrases", out var headerPhrases) && varHeaders.TryGetValue("phraseVoiceNames", out var headerVoiceNames) && 
                varHeaders.TryGetValue("phraseSequenceIds", out var headerPhraseSequenceIds) && varHeaders.TryGetValue("phraseFileTimes", out var headerPhraseFileTimes))
            {
                try
                {
                    if (phrases.Length >= 1)
                    {
                        int[] sequenceIdsArray = new int[phrases.Length];
                        Int64[] fileTimesArray = new Int64[phrases.Length];
                        string[] voiceNamesArray = new string[phrases.Length];
                        string[] phrasesArray = new string[phrases.Length];
                        int[] voiceTypeArray = new int[phrases.Length];
                        for (int i = 0; i < phrases.Length; i++)
                        {
                            sequenceIdsArray[i] = phrases[i].phraseSequenceId;
                            fileTimesArray[i] = phrases[i].fileTime;
                            voiceNamesArray[i] = phrases[i].voiceName;
                            phrasesArray[i] = phrases[i].phrase;
                            voiceTypeArray[i] = phrases[i].voiceType;
                        }
                        UpdateVariable("phraseSequenceIds", sequenceIdsArray);
                        UpdateVariable("phraseFileTimes", fileTimesArray);
                        UpdateVariable("phraseVoiceNames", voiceNamesArray);
                        UpdateVariable("phrasePhrases", phrasesArray);
                        UpdateVariable("phrasesVoiceType", voiceTypeArray);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

    }
}
