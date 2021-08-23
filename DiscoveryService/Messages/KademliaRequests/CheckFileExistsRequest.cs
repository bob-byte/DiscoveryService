using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class CheckFileExistsRequest : Request
    {
        public CheckFileExistsRequest()
        {
            MessageOperation = MessageOperation.CheckFileExists;
        }

        public String OriginalName { get; set; }
    }
}
