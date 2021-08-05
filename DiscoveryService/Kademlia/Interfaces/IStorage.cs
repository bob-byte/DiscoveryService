using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    public interface IStorage
    {
        bool Contains(ID key);
        bool TryGetValue(ID key, out string val);
        string Get(ID key);
        string Get(BigInteger key);
        DateTime GetTimeStamp(BigInteger key);
        void Set(ID key, string value, int expirationTimeSec = 0);
        int GetExpirationTimeSec(BigInteger key);
        void Remove(BigInteger key);
        List<BigInteger> Keys { get; }

        /// <summary>
        /// Updates the republish timestamp.
        /// </summary>
        void Touch(BigInteger key);
    }
}
