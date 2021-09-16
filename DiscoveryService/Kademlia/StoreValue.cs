using System;

namespace LUC.DiscoveryService.Kademlia
{
    public class StoreValue
    {
        public String Value { get; set; }

        public DateTime RepublishTimeStamp { get; set; }

        public Int32 ExpirationTime { get; set; }
    }
}
