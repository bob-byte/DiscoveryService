using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class FindNodeResponse : Response
    {
        public FindNodeResponse( BigInteger randomId )
            : base( randomId )
        {
            DefaultInit();
        }

        public ICollection<Contact> CloseSenderContacts { get; set; }

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

        public override String ToString()
        {
            using ( StringWriter writer = new StringWriter() )
            {
                writer.WriteLine( $"{GetType().Name}:\n" +
                                 $"{PropertyWithValue( nameof( RandomID ), RandomID )};\n" +
                                 $"{nameof( CloseSenderContacts )}:" );

                if ( CloseSenderContacts != null )
                {
                    foreach ( Contact closeContact in CloseSenderContacts )
                    {
                        writer.WriteLine( $"Close contact: {closeContact}\n" );
                    }
                }

                return writer.ToString();
            }
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.FindNodeResponse;
    }
}
