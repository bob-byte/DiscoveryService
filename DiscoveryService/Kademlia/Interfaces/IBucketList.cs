using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia.Interfaces
{
    interface IBucketList
    {
        List<KBucket> Buckets { get; }
        IDht Dht { get; set; }
        KademliaId OurID { get; set; }
        Contact OurContact { get; set; }
        void AddContact( Contact contact );
        KBucket GetKBucket( KademliaId otherID );
        List<Contact> GetCloseContacts( KademliaId key, KademliaId exclude );
        Boolean ContactExists( Contact contact );
    }
}
