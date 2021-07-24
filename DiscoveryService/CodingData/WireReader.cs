using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;

namespace LUC.DiscoveryService.CodingData
{
    /// <summary>
    /// Methods to read DNS wire formatted data items.
    /// </summary>
    public class WireReader : Binary, IDisposable
    {
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
        ///   Read unsigned integer of 16 bits
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public UInt16 ReadUInt16()
        {
            Int32 value = ReadByte();

            Int32 bitInByte = 8;
            value = (value << bitInByte | ReadByte());

            return (UInt16)value;
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
        ///   Read <seealso cref="BigInteger"/>
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public BigInteger ReadBigInteger()
        {
            UInt32 countOfBytes = ReadUInt32();
            BigInteger value = ReadByte();

            for (Int32 numByte = 1; numByte < countOfBytes; numByte++)
            {
                value = value << BitsInOneByte | ReadByte();
            }

            return value;
        }

        /// <summary>
        ///   Read unsigned integer of 32 bits
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public Boolean ReadBoolean() =>
            Convert.ToBoolean(ReadByte());

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
        ///   Strings are encoded with a length prefixed byte. All strings are in ASCII format.
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
            if(!bytes.Any(c => c > MaxValueCharInAscii))
            {
                return Encoding.ASCII.GetString(bytes);
            }
            else
            {
                throw new InvalidDataException("Only ASCII characters are allowed");
            }
        }

        /// <summary>
        /// Read string list of rank 1
        /// (jagged arrays not supported atm)
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public List<String> ReadListOfStrings()
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

        /// <summary>
        /// Read string list of rank 1
        /// (jagged arrays not supported atm)
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public List<Contact> ReadListOfContacts()
        {
            List<Contact> list = new List<Contact>();
            var length = ReadUInt32();
            if (length > 0)
            {
                for (Int32 i = 0; i < length; i++)
                {
                    list.Add(ReadContact());
                }
            }

            return list;
        }

        public Contact ReadContact()
        {
            var idAsBigInt = BigInteger.Parse(ReadString());

            var tcpPort = ReadUInt16();
            var addressesCount = ReadUInt32();

            ICollection<IPAddress> addresses = new List<IPAddress>((Int32)addressesCount);
            for (Int32 numAddress = 0; numAddress < addressesCount; numAddress++)
            {
                addresses.Add(IPAddress.Parse(ReadString()));
            }

            Contact contact = new Contact(new ID(idAsBigInt), tcpPort, addresses);
            return contact;
        }

        public void Dispose() =>
            stream.Close();
    }
}
