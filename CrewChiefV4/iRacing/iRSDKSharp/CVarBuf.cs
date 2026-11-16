using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.IO;

namespace iRSDKSharp
{
    public class CVarBuf
    {
        public const int VarHeaderOffset = 56;
        public int VarHeaderSize = 144;
        //VarBuf offsets
        public int VarBufOffset = 48;
        public const int VarTickCountOffset = 0;
        public const int VarBufOffsetOffset = 4;
        public int VarBufSize = 32;

        MemoryMappedViewAccessor FileMapView = null;
        CiRSDKHeader Header = null;

        public CVarBuf(MemoryMappedViewAccessor mapView, CiRSDKHeader header)
        {
            FileMapView = mapView;
            Header = header;
            VarHeaderSize = Marshal.SizeOf(typeof(VarHeader));
            VarBufSize = Marshal.SizeOf(typeof(VarBuf));
        }
        public int OffsetLatest
        {
            get { return TryGetLatest(out _, out _, out int offset) ? offset : -1; }
        }

        internal bool TryGetLatest(out int descriptor, out int tick, out int offset)
        {
            descriptor = -1;
            tick = 0;
            offset = -1;
            int count = Header.BufferCount;
            if (count < 1 || count > Defines.MaxBufs ||
                VarBufOffset + (long)count * VarBufSize > FileMapView.Capacity)
                return false;

            for (int i = 0; i < count; i++)
            {
                int address = VarBufOffset + i * VarBufSize;
                int candidate = FileMapView.ReadInt32(address + VarTickCountOffset);
                // A difference between concurrent slots is small even across int wrap.
                if (i == 0 || unchecked(candidate - tick) > 0)
                {
                    descriptor = address;
                    tick = candidate;
                }
            }
            offset = FileMapView.ReadInt32(descriptor + VarBufOffsetOffset);
            return IsStillPublished(descriptor, tick, offset);
        }

        internal bool IsStillPublished(int descriptor, int tick, int offset)
        {
            return FileMapView.ReadInt32(descriptor + VarBufOffsetOffset) == offset &&
                FileMapView.ReadInt32(descriptor + VarTickCountOffset) == tick;
        }
    }
}
