using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class FindNodeResponse : Response
    {
        private UInt16 m_tcpPort;

        public FindNodeResponse( Byte[] receivedBytes )
            : base( receivedBytes )
        {
            ;//do nothing
        }

        public FindNodeResponse( BigInteger randomId, UInt16 tcpPort, List<String> bucketIds )
            : base( randomId )
        {
            DefaultInit( tcpPort, bucketIds );
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
        /// Names of groups, which user belongs to
        /// </summary>
        public List<String> BucketIds { get; set; }


        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal ICollection<IContact> CloseSenderContacts { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                BucketIds = reader.ReadListOfAsciiStrings();
                CloseSenderContacts = reader.ReadListOfContacts( DsConstants.LAST_SEEN_FORMAT );

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

                writer.WriteEnumerable( BucketIds );
                writer.WriteEnumerable( CloseSenderContacts, DsConstants.LAST_SEEN_FORMAT );

                writer.Write( TcpPort );
            }
            else
            {
                throw new ArgumentNullException( nameof( writer ) );
            }
        }

        public override String ToString() =>
            Display.ResponseWithCloseContacts( this, CloseSenderContacts );

        protected override void DefaultInit( params Object[] args )
        {
            MessageOperation = MessageOperation.FindNodeResponse;
            CloseSenderContacts = new List<IContact>();

            if ( args.Length == 2)
            {
                TcpPort = (UInt16)args[ 0 ];
                BucketIds = args[ 1 ] as List<String>;
            }
            else
            {
                m_tcpPort = 0;
                BucketIds = new List<String>();
            }
        }
    }
}
