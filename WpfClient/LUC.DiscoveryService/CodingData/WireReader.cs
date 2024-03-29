﻿using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages;
using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;

namespace LUC.DiscoveryServices.CodingData
{
    /// <summary>
    /// Methods to read and wire formatted data items.
    /// </summary>
    public sealed class WireReader : Binary, IDisposable
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="WireReader"/> on the
        ///   specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        ///   The source for data items.
        /// </param>
        public WireReader( Stream stream )
        {
            m_stream = stream;
        }

        private Int32 m_position;

        /// <summary>
        ///   The reader relative position within the stream.
        /// </summary>
        private Int32 GetPosition() => m_position;

        /// <summary>
        ///   The reader relative position within the stream.
        /// </summary>
        private void SetPosition( Int32 value ) => m_position = value;

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
            Int32 value = m_stream.ReadByte();
            if ( value < 0 )
            {
                throw new EndOfStreamException();
            }

            SetPosition( GetPosition() + 1 );
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
            UInt16 value = (UInt16)ReadNumValue( countBytes: 2 );
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
            UInt32 value = (UInt32)ReadNumValue( countBytes: 4 );
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
            UInt64 value = ReadNumValue( countBytes: 8 );
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
            Byte countOfBytes = ReadByte();

            Byte[] bytes = ReadBytes( countOfBytes );

            var bigInt = new BigInteger( bytes );
            return bigInt;
        }

        /// <summary>
        ///   Read unsigned integer of 32 bits
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public Boolean ReadBoolean() =>
            Convert.ToBoolean( ReadByte() );

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
        public Byte[] ReadBytes( Int32 length )
        {
            Byte[] buffer = new Byte[ length ];
            Int32 countReadBytes;
            for ( Int32 offset = 0; length > 0; offset += countReadBytes,
                                               length -= countReadBytes,
                                               SetPosition( GetPosition() + countReadBytes ) )
            {
                countReadBytes = m_stream.Read( buffer, offset, length );
                if ( countReadBytes == 0 )
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
            return ReadBytes( length );
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
            Byte[] bytes = ReadByteLengthPrefixedBytes();
            return Encoding.ASCII.GetString( bytes );
        }

        public String ReadString(Encoding encoding)
        {
            UInt32 bytesCount = ReadUInt32();
            Byte[] bytes = ReadBytes( (Int32)bytesCount );

            return encoding.GetString( bytes );
        }

        public String ReadUtf32String()
        {
            UInt32 bytesCount = ReadUInt32();
            Byte[] bytes = ReadBytes( (Int32)bytesCount );

            return Encoding.UTF32.GetString( bytes );
        }

        /// <summary>
        /// Read string list of rank 1
        /// (jagged arrays not supported atm)
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public List<String> ReadListOfAsciiStrings()
        {
            List<String> list = ReadListOfStrings( ReadAsciiString );

            return list;
        }

        /// <summary>
        /// Read string list of rank 1
        /// (jagged arrays not supported atm)
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public List<String> ReadListOfUtf32Strings()
        {
            List<String> list = ReadListOfStrings( ReadUtf32String );

            return list;
        }

        /// <summary>
        /// Read string list of rank 1
        /// (jagged arrays not supported atm)
        /// </summary>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        public List<String> ReadListOfStrings( Func<String> readOneString )
        {
            var list = new List<String>();
            UInt32 length = ReadUInt32();
            if ( length > 0 )
            {
                for ( Int32 numString = 0; numString < length; numString++ )
                {
                    String newString = readOneString();
                    list.Add( newString );
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
        public List<IContact> ReadListOfContacts( String lastSeenFormat )
        {
            var list = new List<IContact>();
            UInt32 length = ReadUInt32();
            if ( length > 0 )
            {
                for ( Int32 numContact = 0; numContact < length; numContact++ )
                {
                    IContact contact = ReadContact( lastSeenFormat );
                    list.Add( contact );
                }
            }

            return list;
        }

        public IContact ReadContact( String lastSeenFormat )
        {
            String machineId = ReadAsciiString();
            BigInteger idAsBigInt = ReadBigInteger();
            UInt16 tcpPort = ReadUInt16();

            String lastSeenAsStr = ReadAsciiString();

            //don't change next row
            var lastSeen = DateTime.ParseExact( lastSeenAsStr, lastSeenFormat, provider: null );

            //TODO: change to writing IP-address as bytes(see method IPAddress.GetAddressBytes)
            List<String> addressesAsStr = ReadListOfAsciiStrings();
            ICollection<IPAddress> addresses = new List<IPAddress>( addressesAsStr.Count );
            foreach ( String strAddress in addressesAsStr )
            {
                var ipAddress = IPAddress.Parse( strAddress );
                addresses.Add( ipAddress );
            }

            List<String> bucketLocalNames = ReadListOfUtf32Strings();

            var contact = new Contact( machineId, new KademliaId( idAsBigInt ), tcpPort, addresses, lastSeen, bucketLocalNames );
            return contact;
        }

        public ChunkRange ReadRange()
        {
            UInt64 start = ReadUInt64();
            UInt64 end = ReadUInt64();
            UInt64 total = ReadUInt64();

            var readRange = new ChunkRange( start, end, total );
            return readRange;
        }

        public void Dispose() =>
            m_stream.Close();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value">
        /// Already read value
        /// </param>
        /// <returns></returns>
        private UInt64 ReadNumValue( Int32 countBytes )
        {
            UInt64 value = ReadByte();

            //numByte starts from 1 because one byte has been already read
            for ( Int32 numByte = 1; numByte < countBytes; numByte++ )
            {
                value = ( value << BITS_IN_ONE_BYTE ) | ReadByte();
            }

            return value;
        }
    }
}
