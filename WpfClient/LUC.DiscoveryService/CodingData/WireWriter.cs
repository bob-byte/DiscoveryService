﻿using LUC.DiscoveryServices.Common;
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
    /// Methods to write wire formatted data items.
    /// </summary>
    public sealed class WireWriter : Binary, IDisposable
    {
        /// <summary>
        /// Creates a new instance of the <see cref="WireWriter"/> on the
        /// specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">
        /// The destination for data items.
        /// </param>
        public WireWriter( Stream stream )
        {
            m_stream = stream;
        }

        /// <summary>
        /// The writer relative position within the stream.
        /// </summary>
        public Int32 Position { get; private set; }

        public void Dispose() =>
                    m_stream.Close();

        /// <summary>
        /// Write a byte.
        /// </summary>
        /// <param name="value">
        /// Value to write
        /// </param>
        public void Write( Byte value )
        {
            m_stream.WriteByte( value );
            ++Position;
        }

        /// <summary>
        ///   Writes a two-byte unsigned integer to the current stream and advances the stream position by two bytes.
        /// </summary>
        /// <param name="value">
        /// The two-byte unsigned integer to write.
        /// </param>
        public void Write( UInt16 value )
        {
            Write( countOfBits: 16, ( m_byte ) => (Byte)( value >> m_byte ) );

            Position += 2;
        }

        /// <summary>
        ///   Writes a four-byte unsigned integer to the current stream and advances the stream position by four bytes.
        /// </summary>
        /// <param name="value">
        /// The four-byte unsigned integer to write.
        /// </param>
        public void Write( UInt32 value )
        {
            Write( countOfBits: 32, ( m_byte ) => (Byte)( value >> m_byte ) );

            Position += 4;
        }

        /// <summary>
        ///   Writes a eight-byte unsigned integer to the current stream and advances the stream position by eight bytes.
        /// </summary>
        /// <param name="value">
        /// The eight-byte unsigned integer to write.
        /// </param>
        public void Write( UInt64 value )
        {
            Write( countOfBits: 64, ( m_byte ) => (Byte)( value >> m_byte ) );

            Position += 8;
        }

        public void Write( BigInteger value )
        {
            Byte[] bytes = value.ToByteArray();

            Write( (Byte)bytes.Length );
            WriteBytes( bytes );

            Position += bytes.Length;
        }

        public void Write( Boolean value )
        {
            m_stream.WriteByte( Convert.ToByte( value ) );

            Position += 1;
        }

        public void Write( ChunkRange range )
        {
            if ( range != null )
            {
                Write( range.Start );
                Write( range.End );
                Write( range.Total );
            }
            else
            {
                throw new ArgumentNullException( $"{nameof( range )} is null" );
            }
        }

        /// <summary>
        /// Writes a length-prefixed string to this stream in the current encoding of the WireWriter, and advances the current position of the stream in accordance with the encoding used and the specific characters being written to the stream.
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
        /// A rollback has occurred (see the article Character encoding in .NET for a full explanation)
        /// </exception>
        public void WriteAsciiString( String value )
        {
            if ( value != null )
            {
                Byte[] bytes = Encoding.ASCII.GetBytes( value );
                WriteByteLengthPrefixedBytes( bytes );
            }
            else
            {
                throw new ArgumentNullException( nameof( value ) );
            }
        }

        public void Write(String value, Encoding encoding)
        {
            if ( value != null )
            {
                Byte[] bytes = encoding.GetBytes( value );

                UInt32 bytesCount = (UInt32)bytes.Length;
                if ( bytesCount > Int32.MaxValue )
                {
                    throw new ArgumentException( $"Length can\'t exceed {Int32.MaxValue}", nameof( value ) );
                }

                Write( bytesCount );
                WriteBytes( bytes );
            }
            else
            {
                throw new ArgumentNullException( nameof( value ) );
            }
        }

        /// <summary>
        ///   Write a sequence of bytes prefixed with the length as a byte.
        /// </summary>
        /// <param name="bytes">
        ///   A sequence of bytes to write.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   When the length is greater than <see cref="Byte.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Whne <paramref name="bytes"/> is equal to null 
        /// </exception>
        public void WriteByteLengthPrefixedBytes( Byte[] bytes )
        {
            if ( bytes != null )
            {
                Int32 length = bytes.Length;
                if ( length <= Byte.MaxValue )
                {
                    Write( (Byte)length );
                    WriteBytes( bytes );
                }
                else
                {
                    throw new ArgumentException( $"Length can\'t exceed {Byte.MaxValue}", nameof( bytes ) );
                }
            }
            else
            {
                throw new ArgumentNullException( nameof( bytes ) );
            }
        }

        /// <summary>
        /// Write a sequence of bytes.
        /// </summary>
        /// <param name="bytes">
        /// A sequence of bytes to write.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="bytes"/> is equal to null
        /// </exception>
        public void WriteBytes( Byte[] bytes )
        {
            if ( bytes != null )
            {
                m_stream.Write( bytes, offset: 0, bytes.Length );
                Position += bytes.Length;
            }
            else
            {
                throw new ArgumentNullException( nameof( bytes ) );
            }
        }

        /// <summary>
        /// Writes enumerable of data
        /// </summary>
        /// <param name="enumerable">
        /// Enumerable to write
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="enumerable"/> is equal to null
        /// </exception>
        public void WriteEnumerable( IEnumerable<String> enumerable )
        {
            if ( enumerable != null )
            {
                Write( (UInt32)enumerable.Count() );

                foreach ( String item in enumerable )
                {
                    WriteAsciiString( item );
                }
            }
            else
            {
                throw new ArgumentNullException( nameof( enumerable ) );
            }
        }

        /// <summary>
        /// Writes enumerable of data
        /// </summary>
        /// <param name="enumerable">
        /// Enumerable to write
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="enumerable"/> is equal to null
        /// </exception>
        public void WriteEnumerable( IEnumerable<IContact> enumerable, String lastSeenFormat )
        {
            if ( enumerable != null )
            {
                Write( (UInt32)enumerable.Count() );

                foreach ( IContact contact in enumerable )
                {
                    Write( contact, lastSeenFormat );
                }
            }
            else
            {
                throw new ArgumentNullException( nameof( enumerable ) );
            }
        }

        /// <summary>
        /// Writes a length-prefixed string to this stream in the current encoding of the WireWriter, and advances the current position of the stream in accordance with the encoding used and the specific characters being written to the stream.
        /// </summary>
        /// <param name="value">
        /// The value to write.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="value"/> is equal to null
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// A rollback has occurred (see the article Character encoding in .NET for a full explanation)
        /// </exception>
        public void WriteUtf32String( String value )
        {
            if ( value != null )
            {
                Byte[] bytes = Encoding.UTF32.GetBytes( value );

                UInt32 bytesCount = (UInt32)bytes.Length;
                if ( bytesCount > Int32.MaxValue )
                {
                    throw new ArgumentException( $"Length can\'t exceed {Int32.MaxValue}", nameof( bytes ) );
                }

                Write( bytesCount );
                WriteBytes( bytes );
            }
            else
            {
                throw new ArgumentNullException( nameof( value ) );
            }
        }

        private void Write( IContact contact, String lastSeenFormat )
        {
            WriteAsciiString( contact.MachineId );
            Write( contact.KadId.Value );

            Write( contact.TcpPort );
            WriteAsciiString( contact.LastSeen.ToString( lastSeenFormat ) );

            Write( (UInt32)contact.IpAddressesCount );
            if ( contact.IpAddressesCount > 0 )
            {
                List<IPAddress> addresses = contact.IpAddresses();

                foreach ( IPAddress address in addresses )
                {
                    WriteAsciiString( address.ToString() );
                }
            }

            List<String> supportedBuckets = contact.Buckets();
            Write( (UInt32)supportedBuckets.Count );
            if ( supportedBuckets.Count > 0 )
            {
                foreach ( String bucket in supportedBuckets )
                {
                    WriteUtf32String( bucket );
                }
            }
        }
        private void Write( Int32 countOfBits, Func<Int32, Byte> valueToWrite )
        {
            for ( Int32 numsBit = countOfBits - BITS_IN_ONE_BYTE; numsBit >= 0; numsBit -= BITS_IN_ONE_BYTE )
            {
                m_stream.WriteByte( valueToWrite( numsBit ) );
            }
        }
    }
}