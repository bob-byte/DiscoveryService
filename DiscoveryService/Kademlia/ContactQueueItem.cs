using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia
{
    public class ContactQueueItem
    {
        public KademliaId Key { get; set; }
        public IContact IContact { get; set; }
        public Func<KademliaId, IContact, (List<IContact> contacts, IContact foundBy, String val)> RpcCall { get; set; }
        public List<IContact> CloserContacts { get; set; }
        public List<IContact> FartherContacts { get; set; }
        public FindResult FindResult { get; set; }
    }
}
