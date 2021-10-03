using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Interfaces;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class FindValueRequest : Request
    {
        public FindValueRequest( BigInteger sender )
            : base( sender )
        {
            DefaultInit();
        }

        public FindValueRequest()
        {
            DefaultInit();
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
