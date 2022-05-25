using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia
{
    public class FindResult
    {
        public Boolean Found { get; set; }

        public List<IContact> FoundContacts { get; set; }

        public IContact FoundBy { get; set; }

        public String FoundValue { get; set; }
    }
}
