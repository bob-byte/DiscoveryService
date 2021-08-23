using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class DownloadFileRequest : Request
    {
        public String FileOriginalName { get; set; }

        public String Prefix { get; set; }

        public ContantRange ContantRange { get; set; }
    }
}
