using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;

using System;
using System.Text;

namespace LUC.DiscoveryServices.Messages
{
    /// <summary>
    /// <b>Abstract</b> class for discovery messages
    /// </summary>
    internal abstract class DiscoveryMessage : Message
    {
        private UInt16 m_tcpPort;

        protected DiscoveryMessage( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        /// <summary>
        ///   Create a new instance of the <see cref="DiscoveryMessage"/> class.
        /// </summary>
        /// <param name="messageId">
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </param>
        /// <param name="machineId">
        ///   Unique computer configuration identifier
        /// </param>
        /// <param name="protocolVersion">
        ///   Supported version of protocol
        /// </param>
        protected DiscoveryMessage( UInt32 messageId, String machineId,  UInt16 protocolVersion, UInt16 tcpPort )
        {
            MessageId = messageId;
            MachineId = machineId;
            ProtocolVersion = protocolVersion;
            TcpPort = tcpPort;
        }

        /// <summary>
        /// TCP port which is being run in machine with machineId.
        /// TCP port for inter-service communications.
        /// </summary>
        public UInt16 TcpPort 
        {
            get => m_tcpPort;
            set
            {
                AbstractDsData.CheckTcpPort( value );
                m_tcpPort = value;
            }
        }

        /// <summary>
        ///   Unique message identifier. It is used to detect duplicate messages.
        /// </summary>
        public UInt32 MessageId { get; protected set; }

        /// <summary>
        /// Id of machine which is sending this message
        /// </summary>
        public String MachineId { get; protected set; }

        /// <summary>
        ///   Supported version of protocol of the remote application.
        /// </summary>
        public UInt16 ProtocolVersion { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                MessageId = reader.ReadUInt32();
                MachineId = reader.ReadString( Encoding.UTF8 );

                ProtocolVersion = reader.ReadUInt16();
                TcpPort = reader.ReadUInt16();

                return this;
            }
            else
            {
                throw new ArgumentNullException( nameof( reader ) );
            }
        }

        /// <inheritdoc/>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( MessageId );
                writer.Write( MachineId, Encoding.UTF8 );

                writer.Write( ProtocolVersion );
                writer.Write( TcpPort );
            }
            else
            {
                throw new ArgumentNullException( nameof( writer ) );
            }
        }
    }
}
