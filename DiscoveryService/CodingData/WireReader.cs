using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;
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
    /// Methods to read wire formatted data items.
    /// </summary>
    class WireReader : Binary, IDisposable
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
            Int32 value = stream.ReadByte();
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
            UInt16 value = ReadByte();

            value = (UInt16)ReadRestNumValue(value, countBytes: 2);
            return value;
        }

        /// <summary>
        ///   Read unsigned integer of 32 bits
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public UInt32 ReadUInt32()
        {
            UInt32 value = ReadByte();

            value = (UInt32)ReadRestNumValue(value, countBytes: 4);
            return value;
        }

        /// <summary>
        ///   Read unsigned integer of 32 bits
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public UInt64 ReadUInt64()
        {
            UInt64 value = ReadByte();

            value = ReadRestNumValue(value, countBytes: 8);
            return value;
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

            Byte[] bytes = new Byte[countOfBytes];
            for (Int32 numByte = 0; numByte < countOfBytes; numByte++)
            {
                bytes[numByte] = ReadByte();
            }

            var bigInt = new BigInteger(bytes);
            return bigInt;
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
        public String ReadAsciiString()
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

        public String ReadUtf32String()
        {
            Byte[] bytes = ReadByteLengthPrefixedBytes();
            
            return Encoding.UTF32.GetString(bytes);
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
                    list.Add(ReadAsciiString());
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
        public List<Contact> ReadListOfContacts(String lastSeenFormat)
        {
            List<Contact> list = new List<Contact>();
            var length = ReadUInt32();
            if (length > 0)
            {
                for (Int32 i = 0; i < length; i++)
                {
                    list.Add(ReadContact(lastSeenFormat));
                }
            }

            return list;
        }

        //class DateTimeFormat : IFormatProvider
        //{
        //    public Object GetFormat(Type type)
        //    {

        //    }
        //}

        public Contact ReadContact(String lastSeenFormat)
        {
            var machineId = ReadAsciiString();
            var idAsBigInt = ReadBigInteger();
            var tcpPort = ReadUInt16();
            var lastSeen = DateTime.ParseExact(ReadAsciiString(), lastSeenFormat, provider: null);
            
            var addressesCount = ReadUInt32();
            ICollection<IPAddress> addresses = new List<IPAddress>((Int32)addressesCount);
            for (Int32 numAddress = 0; numAddress < addressesCount; numAddress++)
            {
                addresses.Add(IPAddress.Parse(ReadAsciiString()));
            }

            Contact contact = new Contact(machineId, new ID(idAsBigInt), tcpPort, addresses, lastSeen);
            return contact;
        }

        public Range ReadRange()
        {
            var start = ReadUInt64();
            var end = ReadUInt64();
            var total = ReadUInt64();

            var readRange = new Range(start, end, total);
            return readRange;
        }

        public void Dispose() =>
            stream.Close();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="readValue">
        /// Already read value
        /// </param>
        /// <returns></returns>
        private UInt64 ReadRestNumValue(UInt64 readValue, Int32 countBytes)
        {
            //numByte starts from 1 because one byte should be already read
            for (Int32 numByte = 1; numByte < countBytes; numByte++)
            {
                readValue = readValue << BitsInOneByte | ReadByte();
            }

            return readValue;
        }
    }
}
