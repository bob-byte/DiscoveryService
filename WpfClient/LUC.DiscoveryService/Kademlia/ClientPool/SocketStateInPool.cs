using System;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    public enum SocketStateInPool : Byte
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
