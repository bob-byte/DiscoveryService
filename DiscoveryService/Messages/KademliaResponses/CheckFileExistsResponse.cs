using LUC.DiscoveryServices.CodingData;
using LUC.DiscoveryServices.Kademlia;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;

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
    class CheckFileExistsResponse : AbstactFileResponse
    {
        public CheckFileExistsResponse( BigInteger requestRandomId )
            : base( requestRandomId )
        {
            DefaultInit();
        }

        protected override void DefaultInit( params Object[] args ) => 
            MessageOperation = MessageOperation.CheckFileExistsResponse;
    }
}
