using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;

// 
//  Read/Write string and byte arrays 
// 
namespace SilentOrbit.ProtocolBuffers
{
    public interface IProto
    {
        void ReadFromStream(Stream stream, int size);

        void WriteToStream(Stream stream);
    }
    
    public static partial class ProtocolParser
    {
        private static byte[] staticBuffer = new byte[131072];

        public static string ReadString(Stream stream)
        {
            return Encoding.UTF8.GetString(ReadBytes(stream));
        }

        /// <summary>
        /// Reads a length delimited byte array
        /// </summary>
        public static byte[] ReadBytes(Stream stream)
        {
            //VarInt length
            int length = (int)ReadUInt32(stream);

            //Bytes
            byte[] buffer = new byte[length];
            int read = 0;
            while (read < length)
            {
                int r = stream.Read(buffer, read, length - read);
                if (r == 0)
                    throw new ProtocolBufferException("Expected " + (length - read) + " got " + read);
                read += r;
            }
            return buffer;
        }

        /// <summary>
        /// Skip the next varint length prefixed bytes.
        /// Alternative to ReadBytes when the data is not of interest.
        /// </summary>
        public static void SkipBytes(Stream stream)
        {
            int length = (int)ReadUInt32(stream);
            if (stream.CanSeek)
                stream.Seek(length, SeekOrigin.Current);
            else
                ReadBytes(stream);
        }

        public static void WriteString(Stream stream, string val)
        {
            WriteBytes(stream, Encoding.UTF8.GetBytes(val));
        }

        /// <summary>
        /// Writes length delimited byte array
        /// </summary>
        public static void WriteBytes(Stream stream, byte[] val)
        {
            WriteUInt32(stream, (uint)val.Length);
            stream.Write(val, 0, val.Length);
        }

        public static unsafe float ReadSingle(Stream stream)
        {
            stream.Read(ProtocolParser.staticBuffer, 0, 4);
            fixed (byte* numPointer = &staticBuffer[0])
            {
                return *(float*)numPointer;
            }
        }
        
        public static unsafe void WriteSingle(Stream stream, float f)
        {
            byte* numPointer = (byte*)(&f);
            ProtocolParser.staticBuffer[0] = *numPointer;
            ProtocolParser.staticBuffer[1] = *(numPointer + 1);
            ProtocolParser.staticBuffer[2] = *(numPointer + 2);
            ProtocolParser.staticBuffer[3] = *(numPointer + 3);
            stream.Write(ProtocolParser.staticBuffer, 0, 4);
        }
        
        public static unsafe double ReadDouble(Stream stream)
        {
            stream.Read(ProtocolParser.staticBuffer, 0, 8);
            fixed (byte* numPointer = &ProtocolParser.staticBuffer[0])
            {
                return (double)(*numPointer);
            }
        }
        
        public static unsafe void WriteDouble(Stream stream, double f)
        {
            byte* numPointer = (byte*)(&f);
            ProtocolParser.staticBuffer[0] = *numPointer;
            ProtocolParser.staticBuffer[1] = *(numPointer + 1);
            ProtocolParser.staticBuffer[2] = *(numPointer + 2);
            ProtocolParser.staticBuffer[3] = *(numPointer + 3);
            ProtocolParser.staticBuffer[4] = *(numPointer + 4);
            ProtocolParser.staticBuffer[5] = *(numPointer + 5);
            ProtocolParser.staticBuffer[6] = *(numPointer + 6);
            ProtocolParser.staticBuffer[7] = *(numPointer + 7);
            stream.Write(ProtocolParser.staticBuffer, 0, 8);
        }
    }

    /// <summary>
    /// Wrapper for streams that does not support the Position property.
    /// Adds support for the Position property.
    /// </summary>
    public class PositionStream : Stream
    {
        Stream stream;

        /// <summary>
        /// Bytes left to read
        /// </summary>
        public int BytesRead { get; private set; }

        /// <summary>
        /// Define how many bytes are allowed to read
        /// </summary>
        /// <param name='baseStream'>
        /// Base stream.
        /// </param>
        /// <param name='maxLength'>
        /// Max length allowed to read from the stream.
        /// </param>
        public PositionStream(Stream baseStream)
        {
            this.stream = baseStream;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = stream.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int ReadByte()
        {
            int b = stream.ReadByte();
            BytesRead += 1;
            return b;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this.BytesRead;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override void Close()
        {
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            stream.Dispose();
            base.Dispose(disposing);
        }
    }
}

