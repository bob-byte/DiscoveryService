using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class StoreResponse : Response
    {
        public StoreResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        public override String ToString()
        {
            using ( StringWriter writer = new StringWriter() )
            {
                writer.WriteLine( $"{GetType().Name}:\n" +
                                 $"{PropertyWithValue( nameof( RandomID ), RandomID )}" );

                return writer.ToString();
            }
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.StoreResponse;
    }
}
