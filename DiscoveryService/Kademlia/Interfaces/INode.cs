namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    public interface INode
    {
        Contact OurContact { get; }
        IBucketList BucketList { get; }
    }
}
