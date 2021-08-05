namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    //public interface IKBucket
    //{
    //    BigInteger Low { get; }
    //    BigInteger High { get; }
    //}

    public interface IDht
    {
        Node Node { get; set; }
        void DelayEviction(Contact toEvict, Contact toReplace);
        void AddToPending(Contact pending);
    }
}
