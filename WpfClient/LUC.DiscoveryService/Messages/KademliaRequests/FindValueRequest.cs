using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Interfaces;

using System;
using System.Numerics;

namespace LUC.DiscoveryServices.Messages.KademliaRequests
{
    class FindValueRequest : Request
    {
        public FindValueRequest( BigInteger senderKadId, String senderMachineId )
            : base( senderKadId, senderMachineId )
        {
            DefaultInit();
        }

        public FindValueRequest(Byte[] receivedBytes) 
            : base(receivedBytes)
        {
            ;//do nothing
        }

        public BigInteger KeyToFindCloseContacts { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );
                KeyToFindCloseContacts = reader.ReadBigInteger();

                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
            }
        }

        /// <inheritdoc/>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );
                writer.Write( KeyToFindCloseContacts );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        protected override void DefaultInit( params Object[] args ) =>
            MessageOperation = MessageOperation.FindValue;
    }
}
