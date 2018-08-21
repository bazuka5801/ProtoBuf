using System;
using System.Runtime.CompilerServices;

namespace SilentOrbit.ProtocolBuffers
{
    public static class ExMethods
    {
        public static unsafe float ReadFloat(this byte[] buffer, int iOffset = 0)
        {
            fixed (byte* numPointer = &buffer[iOffset])
            {
                return *(float*)numPointer;
            }
        }

        public static unsafe void WriteFloat(this byte[] buffer, float f, int iOffset = 0)
        {
            byte* numPointer = (byte*)(&f);
            buffer[iOffset] = *numPointer;
            buffer[iOffset + 1] = *(numPointer + 1);
            buffer[iOffset + 2] = *(numPointer + 2);
            buffer[iOffset + 3] = *(numPointer + 3);
        }
        
        public static unsafe double ReadDouble(this byte[] buffer, int iOffset = 0)
        {
            fixed (byte* numPointer = &buffer[iOffset])
            {
                return (double)(*numPointer);
            }
        }

        public static unsafe void WriteDouble(this byte[] buffer, double f, int iOffset = 0)
        {
            byte* numPointer = (byte*)(&f);
            buffer[iOffset] = *numPointer;
            buffer[iOffset + 1] = *(numPointer + 1);
            buffer[iOffset + 2] = *(numPointer + 2);
            buffer[iOffset + 3] = *(numPointer + 3);
            buffer[iOffset + 4] = *(numPointer + 4);
            buffer[iOffset + 5] = *(numPointer + 5);
            buffer[iOffset + 6] = *(numPointer + 6);
            buffer[iOffset + 7] = *(numPointer + 7);
        }
    }
}