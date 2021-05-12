using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LUC.DiscoveryService.CodingData
{
    public class WireWriter : IDisposable
    {
        private readonly Stream stream;

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
        ///   The writer relative position within the stream.
        /// </summary>
        public Int32 Position { get; set; }

        /// <summary>
        ///   Write a byte.
        /// </summary>
        public void WriteByte(Byte value)
        {
            stream.WriteByte(value);
            ++Position;
        }

        /// <summary>
        ///   Writes a four-byte unsigned integer to the current stream and advances the stream position by four bytes.
        /// </summary>
        /// <param name="value">
        /// The four-byte unsigned integer to write.
        /// </param>
        public void Write(UInt32 value)
        {
            stream.WriteByte((Byte)(value >> 24));
            stream.WriteByte((Byte)(value >> 16));
            stream.WriteByte((Byte)(value >> 8));
            stream.WriteByte((Byte)value);

            Position += 4;
        }

        /// <summary>
        ///   Write a sequence of bytes.
        /// </summary>
        /// <param name="bytes">
        ///   A sequence of bytes to write.
        /// </param>
        public void WriteBytes(Byte[] bytes)
        {
            if(bytes != null)
            {
                stream.Write(bytes, offset: 0, bytes.Length);
                Position += bytes.Length;
            }
            else
            {
                throw new ArgumentNullException(nameof(bytes));
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
        /// <exception cref="ArgumentNullException">
        /// Whne <paramref name="bytes"/> is equal to null 
        /// </exception>
        public void WriteByteLengthPrefixedBytes(Byte[] bytes)
        {
            if(bytes != null)
            {
                var length = bytes.Length;
                if(length <= Byte.MaxValue)
                {
                    WriteByte((Byte)length);
                    WriteBytes(bytes);
                }
                else
                {
                    throw new ArgumentException($"Length can\'t exceed {Byte.MaxValue}", nameof(bytes));
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(bytes));
            }
        }

        /// <summary>
        /// Writes a length-prefixed string to this stream in the current encoding of the BinaryWriter, and advances the current position of the stream in accordance with the encoding used and the specific characters being written to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <exception cref="ArgumentException">
        /// If <paramref name="value"/> cannot be encoded to ASCII
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="value"/> is equal to null
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// </exception>
        public void Write(String value)
        {
            if(value != null)
            {
                if (!value.Any(c => c > 0x7F))
                {
                    var bytes = Encoding.ASCII.GetBytes(value);
                    WriteByteLengthPrefixedBytes(bytes);
                }
                else
                {
                    throw new ArgumentException("Only ASCII characters are allowed");
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(value));
            }
        }

        public void WriteDictionary(ConcurrentDictionary<String, String> dict)
        {
            WriteEnumerable(dict.Keys);
            WriteEnumerable(dict.Values);
        }

        public void WriteEnumerable(IEnumerable<String> enumerable)
        {
            if (enumerable != null)
            {
                var array = enumerable.ToArray();

                Write((UInt32)array.Length);
                foreach (var item in array)
                {
                    Write(item);
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(enumerable));
            }
        }

        public static IEnumerable<Byte> ToBytes(BitArray bits, Boolean msb = false)
        {
            Int32 bitCount = 7;//why 7, not 8?. For ASCII
            Int32 resultByte = 0;

            foreach (Boolean isOne in bits)
            {
                if(isOne)
                {
                    resultByte |= msb ? 1 << bitCount : 1 << (7 - bitCount);
                }

                if(bitCount == 0)
                {
                    yield return (Byte)resultByte;

                    bitCount = 8;
                    resultByte = 0;
                }
                bitCount = 0;
            }

            if(bitCount < 7)
            {
                yield return (Byte)resultByte;
            }
        }

        public void Dispose() =>
            stream.Close();
    }
}