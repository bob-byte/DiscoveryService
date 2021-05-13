using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LUC.DiscoveryService.CodingData
{
    /// <summary>
    /// Methods to read DNS wire formatted data items.
    /// </summary>
    public class WireReader : IDisposable
    {
        private readonly Stream stream;

        /// <summary>
        ///   Creates a new instance of the <see cref="WireReader"/> on the
        ///   specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        ///   The source for data items.
        /// </param>
        public WireReader(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        ///   The reader relative position within the stream.
        /// </summary>
        public Int32 Position { get; set; }

        /// <summary>
        ///   Read a byte.
        /// </summary>
        /// <returns>
        ///   The next byte in the stream.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public Byte ReadByte()
        {
            var value = stream.ReadByte();
            if(value < 0)
            {
                throw new EndOfStreamException();
            }

            ++Position;
            return (Byte)value;
        }

        /// <summary>
        ///   Read unsigned integer of 32 bits
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public UInt32 ReadUInt32()
        {
            Int32 value = ReadByte();

            Int32 bitInByte = 8;
            value = value << bitInByte | ReadByte();
            value = value << bitInByte | ReadByte();
            value = value << bitInByte | ReadByte();

            return (UInt32)value;
        }

        /// <summary>
        ///   Read the specified number of bytes.
        /// </summary>
        /// <param name="length">
        ///   The number of bytes to read.
        /// </param>
        /// <returns>
        ///   The next <paramref name="length"/> bytes in the stream.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public Byte[] ReadBytes(Int32 length)
        {
            var buffer = new Byte[length];
            Int32 countReadBytes;
            for (Int32 offset = 0; length > 0; offset += countReadBytes, 
                                             length -= countReadBytes, 
                                             Position += countReadBytes)
            {
                countReadBytes = stream.Read(buffer, offset, length);
                if (countReadBytes == 0)
                {
                    throw new EndOfStreamException();
                }
            }

            return buffer;
        }

        /// <summary>
        ///   Read the bytes with a byte length prefix.
        /// </summary>
        /// <returns>
        ///   The next N bytes.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public Byte[] ReadByteLengthPrefixedBytes()
        {
            Int32 length = ReadByte();
            return ReadBytes(length);
        }

        /// <summary>
        ///   Read a string.
        /// </summary>
        /// <remarks>
        ///   Strings are encoded with a length prefixed byte.  All strings are ASCII.
        /// </remarks>
        /// <returns>
        ///   The string.
        /// </returns>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        /// <exception cref="InvalidDataException">
        ///   Only ASCII characters are allowed.
        /// </exception>
        public String ReadString()
        {
            var bytes = ReadByteLengthPrefixedBytes();
            if(!bytes.Any(c => c > 0x7F))
            {
                return Encoding.ASCII.GetString(bytes);
            }
            else
            {
                throw new InvalidDataException("Only ASCII characters are allowed");
            }
        }

        /// <summary>
        /// Read enumerable of rank 1
        /// ( jagged arrays not supported atm )
        /// /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public List<String> ReadListofStrings()
        {
            List<String> list = new List<String>();
            var length = ReadUInt32();
            if (length > 0)
            {
                for (Int32 i = 0; i < length; i++)
                {
                    list.Add(ReadString());
                }
            }

            return list;
        }

        public void Dispose() =>
            stream.Close();
    }
}
