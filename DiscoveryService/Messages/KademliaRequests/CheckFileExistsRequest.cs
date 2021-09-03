using LUC.DiscoveryService.CodingData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class CheckFileExistsRequest : FileRequest
    {
        public CheckFileExistsRequest()
        {
            MessageOperation = MessageOperation.CheckFileExists;//try to put in static constructor
        }
    }
}
