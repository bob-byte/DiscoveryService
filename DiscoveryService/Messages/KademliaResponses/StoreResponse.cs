using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Messages.KademliaRequests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Messages.KademliaResponses
{
    class StoreResponse : Response
    {
        public StoreResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.StoreResponse;
    }
}
