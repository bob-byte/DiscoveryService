namespace LUC.DiscoveryServices.Kademlia.Interfaces
{
    interface INode
    {
        Contact OurContact { get; }
        IBucketList BucketList { get; }
    }
}
