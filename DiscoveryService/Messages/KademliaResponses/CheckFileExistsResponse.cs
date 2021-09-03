using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class CheckFileExistsResponse : FileResponse
    {
        public CheckFileExistsResponse()
        {
            MessageOperation = MessageOperation.CheckFileExistsResponse;
        }
    }
}
