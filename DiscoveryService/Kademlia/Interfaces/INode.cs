using LUC.Interfaces.Discoveries;

using System;
using System.Collections.Generic;

namespace LUC.DiscoveryServices.Kademlia.Interfaces
{
    interface INode
    {
        IContact OurContact { get; }

        IStorage Storage { get; set; }

        IStorage CacheStorage { get; set; }

        void Ping( IContact sender );

        void Store( IContact sender, KademliaId key, String val, Boolean isCached, Int32 expirationTimeSec );

        void FindNode( IContact sender, KademliaId key, out List<IContact> contacts );

        void FindValue( IContact sender, KademliaId key, out List<IContact> contacts, out String nodeValue );
    }
}
