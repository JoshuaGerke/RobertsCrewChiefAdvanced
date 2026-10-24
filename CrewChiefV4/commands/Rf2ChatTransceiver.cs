using System;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace CrewChiefV4.commands
{
    /// <summary>
    /// Use https://github.com/tappi287/rf2_chat_transceiver to send chat text to rF2
    /// </summary>
    public class Rf2ChatTransceiver
    {
        /// <summary>
        /// rf2_chat_transceiver is present and working
        /// </summary>
        public bool isAvailable { get; private set; }

        public Rf2ChatTransceiver()
        {
            isAvailable = Game.RF2_LMU;
        }

        public bool SendChat(string chatText)
        {
            if (isAvailable)
            {
                string sharedMemoryName = "rF2_ChatTransceiver_SM";

                try
                {
                    // Create or open a memory-mapped file
                    using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(sharedMemoryName, MemoryMappedFileRights.Write))
                    {
                        // Create a view accessor to write data to the memory-mapped file
                        using (MemoryMappedViewAccessor accessor = mmf.CreateViewAccessor())
                        {
                            byte[] data = Encoding.GetEncoding("Windows-1252").GetBytes(0 + chatText);

                            // Write data to the shared memory
                            accessor.WriteArray(0, data, 0, data.Length);

                            Log.Commentary("Text written to rF2 chat SM: " + chatText);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                    Log.Error("rFactor 2 Chat Transceiver Plugin not available");
                    isAvailable = false;
                }
            }
            return isAvailable;
        }
    }
}