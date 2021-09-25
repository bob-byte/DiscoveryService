﻿namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    interface IDht
    {
        Node Node { get; set; }
        void DelayEviction( Contact toEvict, Contact toReplace );
        void AddToPending( Contact pending );
    }
}