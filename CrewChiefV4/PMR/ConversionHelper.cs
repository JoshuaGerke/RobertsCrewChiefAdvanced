using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.PMR
{
    public class ConversionHelper
    {
        public static void convertVec(ref byte[] data, ref int startIdx, out UDPVec3 vec)
        {
            vec = new UDPVec3();

            vec.x = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);

            vec.y = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);

            vec.z = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);
        }

        public static void convertQuat(ref byte[] data, ref int startIdx, out UDPQuat quat)
        {
            quat = new UDPQuat();

            quat.x = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);

            quat.y = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);

            quat.z = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);

            quat.w = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);
        }

        public static void convertFloat(ref byte[] data, ref int startIdx, out float f)
        {
            f = BitConverter.ToSingle(data, startIdx);
            startIdx += sizeof(float);
        }

        public static void convertBool(ref byte[] data, ref int startIdx, out bool b)
        {
            byte val = data[startIdx];
            startIdx += sizeof(byte);

            b = val != 0;
        }
        public static void convertByte(ref byte[] data, ref int startIdx, out byte b)
        {
            b = data[startIdx];
            startIdx += sizeof(byte);
        }

        public static void convertInt32(ref byte[] data, ref int startIdx, out Int32 i)
        {
            i = BitConverter.ToInt32(data, startIdx);
            startIdx += sizeof(Int32);
        }

        public static void convertUInt16(ref byte[] data, ref int startIdx, out UInt16 i)
        {
            i = BitConverter.ToUInt16(data, startIdx);
            startIdx += sizeof(UInt16);
        }

        public static void convertUInt32(ref byte[] data, ref int startIdx, out UInt32 i)
        {
            i = BitConverter.ToUInt32(data, startIdx);
            startIdx += sizeof(UInt32);
        }

        public static void convertString(ref byte[] data, ref int startIdx, out string str)
        {
            byte len = 0;
            convertByte(ref data, ref startIdx, out len);

            byte[] characters = new byte[len];
            for (int i = 0; i < len; i++)
            {
                convertByte(ref data, ref startIdx, out characters[i]);
            }

            str = Encoding.UTF8.GetString(characters);
        }

    }
}
