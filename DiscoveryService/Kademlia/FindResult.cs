using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia
{
    public class FindResult
    {
        public Boolean Found { get; set; }

        public List<Contact> FoundContacts { get; set; }

        public Contact FoundBy { get; set; }

        public String FoundValue { get; set; }
    }
}
