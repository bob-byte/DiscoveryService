using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    class StoreRequest : Request
    {
        public BigInteger Key { get; set; }
        public String Value { get; set; }
        public Boolean IsCached { get; set; }
        public Int32 ExpirationTimeSec { get; set; }
    }
}
