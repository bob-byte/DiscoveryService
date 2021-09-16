using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    interface IStorage
    {
        Boolean Contains( KademliaId key );
        Boolean TryGetValue( KademliaId key, out String val );
        String Get( KademliaId key );
        String Get( BigInteger key );
        DateTime GetTimeStamp( BigInteger key );
        void Set( KademliaId key, String value, Int32 expirationTimeSec = 0 );
        Int32 GetExpirationTimeSec( BigInteger key );
        void Remove( BigInteger key );
        List<BigInteger> Keys { get; }

        /// <summary>
        /// Updates the republish timestamp.
        /// </summary>
        void Touch( BigInteger key );
    }
}
