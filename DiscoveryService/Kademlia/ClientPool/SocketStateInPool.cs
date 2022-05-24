namespace DiscoveryServices.Kademlia.ClientPool
{
    public enum SocketStateInPool
    {
        NeverWasInPool,
        TakenFromPool,

        /// <summary>
        /// Something was wrong during some socket operation
        /// </summary>
        IsFailed,
        IsInPool
    }
}
