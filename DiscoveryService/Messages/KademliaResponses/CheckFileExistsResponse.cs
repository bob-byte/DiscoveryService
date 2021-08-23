using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    class CheckFileExistsResponse : Response
    {
        public CheckFileExistsResponse()
        {
            MessageOperation = MessageOperation.CheckFileExistsResponse;
        }

        public Boolean Exist { get; set; }

        public String Version { get; set; }

        public Int64 FileSize { get; set; }
    }
}
