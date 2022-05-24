using DiscoveryServices.CodingData;
using DiscoveryServices.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices.Messages
{
    /// <summary>
    /// Allows to write and read TCP message to/from <see cref="Stream"/>
    /// </summary>
    class AcknowledgeTcpMessage : DiscoveryMessage
    {
        /// <summary>
        /// Create a new instance of the <see cref="AcknowledgeTcpMessage"/> class. This constructor is often used to read message
        /// </summary>
        public AcknowledgeTcpMessage( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        /// <summary>
        ///   Create a new instance of the <see cref="AcknowledgeTcpMessage"/> class. This constructor is often used to write message to a stream
        /// </summary>
        /// <param name="messageId">
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// /// <param name="groupsIds">
        /// Names of user groups
        /// </param>
        /// <param name="tcpPort">
        /// TCP port for Kademilia requests.
        /// </param>
        public AcknowledgeTcpMessage( UInt32 messageId, String machineId, BigInteger idOfSendingContact,
            UInt16 tcpPort, UInt16 protocolVersion, List<String> groupsIds )
            : base( messageId, machineId, protocolVersion )
        {
            DefaultInit( groupsIds );

            TcpPort = tcpPort;
            IdOfSendingContact = idOfSendingContact;
        }

        public BigInteger IdOfSendingContact { get; set; }

        /// <summary>
        /// Names of groups, which node belongs to
        /// </summary>
        public List<String> BucketIds { get; set; }

        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                MessageId = reader.ReadUInt32();
                IdOfSendingContact = reader.ReadBigInteger();
                MachineId = reader.ReadAsciiString();

                ProtocolVersion = reader.ReadUInt16();
                TcpPort = reader.ReadUInt16();
                BucketIds = reader.ReadListOfAsciiStrings();

                return this;
            }
            else
            {
                throw new ArgumentNullException( nameof( reader ) );
            }
        }

        /// <summary>
        /// Write as a binary message.
        /// </summary>
        /// <param name="writer"></param>
        /// <exception cref="ArgumentNullException">
        /// 
        /// </exception>
        /// <exception cref="EncoderFallbackException">
        /// 
        /// </exception>
        /// <exception cref="ArgumentException">
        /// 
        /// </exception>
        /// <exception cref="InvalidDataException">
        /// 
        /// </exception>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( MessageId );
                writer.Write( IdOfSendingContact );
                writer.WriteAsciiString( MachineId );
                writer.Write( ProtocolVersion );
                writer.Write( TcpPort );
                writer.WriteEnumerable( BucketIds );
            }
            else
            {
                throw new ArgumentNullException( nameof( writer ) );
            }
        }

        //public override String ToString() =>
        //    Display.ObjectToString( objectToConvert: this );

        protected override void DefaultInit( params Object[] args )
        {
            MessageOperation = MessageOperation.Acknowledge;

            if ( args.Length > 0 )
            {
                if ( args[ 0 ] is List<String> groupIds )
                {
                    BucketIds = groupIds;
                }
                else
                {
                    BucketIds = new List<String>();
                }
            }
            else
            {
                BucketIds = new List<String>();
            }
        }
    }
}
