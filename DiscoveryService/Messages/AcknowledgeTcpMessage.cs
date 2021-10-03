using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages
{
    /// <summary>
    /// Allows to write and read TCP message to/from <see cref="Stream"/>
    /// </summary>
    class AcknowledgeTcpMessage : DiscoveryServiceMessage
    {
        /// <summary>
        /// Create a new instance of the <see cref="AcknowledgeTcpMessage"/> class. This constructor is often used to read message
        /// </summary>
        public AcknowledgeTcpMessage()
        {
            DefaultInit();
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
        /// Names of groups
        /// </summary>
        public List<String> GroupIds { get; set; }

        public virtual async Task Send( IPEndPoint endPoint, Byte[] bytes )
        {
            TcpClient client = null;
            NetworkStream stream = null;

            try
            {
                client = new TcpClient( endPoint.AddressFamily );

                client.Connect( endPoint.Address, endPoint.Port );

                stream = client.GetStream();
                await stream.WriteAsync( bytes, offset: 0, bytes.Length );
            }
            finally
            {
                client?.Close();
                stream?.Close();
            }
        }

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
                GroupIds = reader.ReadListOfStrings();

                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
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
                writer.WriteEnumerable( GroupIds );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
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
                    GroupIds = groupIds;
                }
                else
                {
                    GroupIds = new List<String>();
                }
            }
            else
            {
                GroupIds = new List<String>();
            }
        }
    }
}
