﻿namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    interface INode
    {
        Contact OurContact { get; }
        IBucketList BucketList { get; }
    }
}