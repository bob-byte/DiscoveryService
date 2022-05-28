using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    class FindNodeRequest : Request
    {
        private UInt16 m_tcpPort;

        public FindNodeRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public FindNodeRequest( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public BigInteger KeyToFindCloseContacts { get; set; }

        /// <summary>
        /// Names of groups, which node belongs to
        /// </summary>
        public List<String> BucketIds { get; set; }

        public UInt16 TcpPort 
        {
            get => m_tcpPort;
            set
            {
                AbstractDsData.CheckTcpPort( value );
                m_tcpPort = value;
            }
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                KeyToFindCloseContacts = reader.ReadBigInteger();
                BucketIds = reader.ReadListOfAsciiStrings();
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

                writer.Write( KeyToFindCloseContacts );
                writer.WriteEnumerable( BucketIds );
                writer.Write( TcpPort );
            }
            else
            {
                throw new ArgumentNullException( nameof( writer ) );
            }
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.FindNode;
    }
}
