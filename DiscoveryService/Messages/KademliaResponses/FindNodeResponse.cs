using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Interfaces;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class FindNodeResponse : Response
    {
        public FindNodeResponse( BigInteger randomId )
            : base( randomId )
        {
            DefaultInit();
        }

        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal ICollection<Contact> CloseSenderContacts { get; set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );
                CloseSenderContacts = reader.ReadListOfContacts( Constants.LAST_SEEN_FORMAT );

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
                writer.WriteEnumerable( CloseSenderContacts, Constants.LAST_SEEN_FORMAT );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        public override String ToString() =>
            Display.ResponseWithCloseContacts( this, CloseSenderContacts );

        protected override void DefaultInit( params Object[] args )
        {
            MessageOperation = MessageOperation.FindNodeResponse;
            CloseSenderContacts = new List<Contact>();
        }
    }
}
