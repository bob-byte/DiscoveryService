﻿using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;
using LUC.Interfaces.Constants;

using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages
{
    class Message : IWireSerialiser
    {
        public const Int32 MIN_LENGTH = 5;//MessageOperation(1 byte) + MessageLength (4 bytes)

        public const Int32 MIN_TCP_CLIENT_MESS_LENGTH = 71;

        public Message( Byte[] receivedBytes )
        {
            Read( receivedBytes );
        }

        public Message()
        {
            ;//do nothing
        }

        /// <summary>
        ///   The kind of message. This value always is first byte in it
        /// </summary>
        public MessageOperation MessageOperation { get; protected set; }

        public UInt32 MessageLength { get; protected set; }

        /// <summary>
        ///   Length in bytes of the object when serialised.
        /// </summary>
        /// <returns>
        ///   Numbers of bytes when serialised.
        /// </returns>
        public Int32 Length()
        {
            var writer = new WireWriter( Stream.Null );
            Write( writer );

            return writer.Position;
        }

        /// <summary>
        ///   Reads the Message object from a byte array.
        /// </summary>
        /// <param name="buffer">
        ///   The source for the Message object.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="buffer"/> is equal to null
        /// </exception>
        public IWireSerialiser Read( Byte[] buffer ) =>
            Read( buffer, offset: 0, buffer.Length );

        /// <summary>
        ///   Reads the DNS object from a byte array.
        /// </summary>
        /// <param name="buffer">
        ///   The source for the DNS object.
        /// </param>
        /// <param name="offset">
        ///   The offset into the <paramref name="buffer"/>.
        /// </param>
        /// <param name="count">
        ///   The number of bytes in the <paramref name="buffer"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="buffer"/> is equal to null
        /// </exception>
        public IWireSerialiser Read( Byte[] buffer, Int32 offset, Int32 count )
        {
            using ( var stream = new MemoryStream( buffer, offset, count ) )
            {
                return Read( new WireReader( stream ) );
            }
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="reader"/> is equal to null
        /// </exception>
        /// <exception cref="EndOfStreamException">
        ///   When no more data is available.
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// If <seealso cref="Message"/>has string(-s) with no ASCII characters.
        /// </exception>
        /// <exception cref="IOException">
        /// 
        /// </exception>
        public virtual IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                MessageOperation = (MessageOperation)reader.ReadByte();
                MessageLength = reader.ReadUInt32();
                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
            }
        }

        public virtual async Task SendAsync( Socket sender )
        {
            sender.SendTimeout = (Int32)DsConstants.SendTimeout.TotalMilliseconds;
            Byte[] buffer = ToByteArray();

            await sender.SendAsync( buffer: new ArraySegment<Byte>( buffer ), SocketFlags.None ).ConfigureAwait( continueOnCapturedContext: false );
#if DEBUG
            DsLoggerSet.DefaultLogger.LogInfo( $"Sent \n{this}" );
#endif
        }

        /// <summary>
        ///   Writes the Message object to a byte array.
        /// </summary>
        /// <returns>
        ///   A byte array containing the binary representaton of the Message object.
        /// </returns>
        public Byte[] ToByteArray()
        {
            using ( var stream = new MemoryStream() )
            {
                Write( stream );

                using ( var streamToWriteMessLength = new MemoryStream() )
                {
                    var allMessage = stream.ToArray().ToList();

                    Int32 bytesInInt = 4;
                    MessageLength = (UInt32)( allMessage.Count + bytesInInt );

                    var writer = new WireWriter( streamToWriteMessLength );
                    writer.Write( MessageLength );

                    Byte[] bytesOfMessageLength = streamToWriteMessLength.ToArray();

                    allMessage.InsertRange( index: 1, bytesOfMessageLength );

                    return allMessage.ToArray();
                }
            }
        }

        /// <summary>
        /// Writes the Message object to a stream.
        /// </summary>
        /// <param name="stream">
        /// The destination for the Message object.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="writer"/> is equal to null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <seealso cref="DiscoveryMessage"/> has string, which cannot be encoded to ASCII
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// A rollback has occurred (see the article Character encoding in .NET for a full explanation)
        /// </exception>
        public void Write( Stream stream ) =>
            Write( new WireWriter( stream ) );

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">
        /// When <paramref name="writer"/> is equal to null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If <seealso cref="DiscoveryMessage"/> has string, which cannot be encoded to ASCII
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// A rollback has occurred (see the article Character encoding in .NET for a full explanation)
        /// </exception>
        public virtual void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                writer.Write( (Byte)MessageOperation );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        public override String ToString()
        {
            if ( MessageLength == 0 )
            {
                MessageLength = (UInt32)Length();
            }

            return Display.ToString( objectToConvert: this );
        }

        protected virtual void DefaultInit( params Object[] args )
        {
            ;//do nothing
        }
    }
}
