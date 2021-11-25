using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia
{
    public class ContactQueueItem
    {
        public KademliaId Key { get; set; }
        public Contact Contact { get; set; }
        public Func<KademliaId, Contact, (List<Contact> contacts, Contact foundBy, String val)> RpcCall { get; set; }
        public List<Contact> CloserContacts { get; set; }
        public List<Contact> FartherContacts { get; set; }
        public FindResult FindResult { get; set; }
    }
}
