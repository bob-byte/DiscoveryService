using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;

namespace DiscoveryServices.Kademlia.Interfaces
{
    interface IBucketList
    {
        List<KBucket> Buckets { get; }
        IDht Dht { get; set; }
        KademliaId OurID { get; set; }
        IContact OurContact { get; set; }
        void AddContact( IContact contact );
        KBucket GetKBucket( KademliaId otherID );

        List<IContact> GetCloseContacts( KademliaId key, String machineIdForExclude );

        Boolean ContactExists( IContact contact );
    }
}
