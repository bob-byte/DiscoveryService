using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaResponses
{
    public class ContactResponse
    {
        public BigInteger Contact { get; set; }
        public object Protocol { get; set; }
        public string ProtocolName { get; set; }
    }
}
