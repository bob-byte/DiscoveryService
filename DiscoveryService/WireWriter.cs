using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Collections;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Methods to write a wire formatted data items.
    /// </summary>
    public class WireWriter
    {
        Stream stream;

        /// <summary>
        ///   The writer relative position within the stream.
        /// </summary>
        public int Position;

        /// <summary>
        ///   Creates a new instance of the <see cref="WireWriter"/> on the
        ///   specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        ///   The destination for data items.
        /// </param>
        public WireWriter(Stream stream)
        {
            this.stream = stream;
        }

        /// <summary>
        ///   Write a byte.
        /// </summary>
        public void WriteByte(byte value)
        {
            stream.WriteByte(value);
            ++Position;
        }

        /// <summary>
        ///   Write a sequence of bytes.
        /// </summary>
        /// <param name="bytes">
        ///   A sequence of bytes to write.
        /// </param>
        public void WriteBytes(byte[] bytes)
        {
            if (bytes != null)
            {
                stream.Write(bytes, 0, bytes.Length);
                Position += bytes.Length;
            }
        }


        /// <summary>
        ///   Write a sequence of bytes prefixed with the length as a byte.
        /// </summary>
        /// <param name="bytes">
        ///   A sequence of bytes to write.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   When the length is greater than <see cref="byte.MaxValue"/>.
        /// </exception>
        public void WriteByteLengthPrefixedBytes(byte[] bytes)
        {
            var length = bytes?.Length ?? 0;
            if (length > byte.MaxValue)
                throw new ArgumentException($"Length can not exceed {byte.MaxValue}.", "bytes");

            WriteByte((byte)length);
            WriteBytes(bytes);
        }

        /// <summary>
        ///   Write a string.
        /// </summary>
        /// <exception cref="ArgumentException">
        ///   When the length is greater than <see cref="byte.MaxValue"/> or
        ///   the string is not ASCII.
        /// </exception>
        /// <remarks>
        ///   Strings are encoded with a length prefixed byte.  All strings must be
        ///   ASCII.
        /// </remarks>
        public void WriteString(string value)
        {
            if (value.Any(c => c > 0x7F))
            {
                throw new ArgumentException("Only ASCII characters are allowed.");
            }

            var bytes = Encoding.ASCII.GetBytes(value);
            WriteByteLengthPrefixedBytes(bytes);
        }

        static IEnumerable<Byte> ToBytes(BitArray bits, bool MSB = false)
        {
            int bitCount = 7;
            int outByte = 0;

            foreach (bool bitValue in bits)
            {
                if (bitValue)
                    outByte |= MSB ? 1 << bitCount : 1 << (7 - bitCount);
                if (bitCount == 0)
                {
                    yield return (byte)outByte;
                    bitCount = 8;
                    outByte = 0;
                }
                bitCount--;
            }
            // Last partially decoded byte
            if (bitCount < 7)
                yield return (byte)outByte;
        }
    }
    }
}