using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;

using LUC.DiscoveryServices.Kademlia.Interfaces;

using Newtonsoft.Json;

namespace LUC.DiscoveryServices.Kademlia
{
    /// <summary>
    /// In-memory storage, used for node cache store if not explicitly specified.
    /// </summary>
    public class VirtualStorage : IStorage
    {
        protected ConcurrentDictionary<BigInteger, StoreValue> m_store;

        public VirtualStorage()
        {
            m_store = new ConcurrentDictionary<BigInteger, StoreValue>();
        }

        [JsonIgnore]
        public List<BigInteger> Keys =>
            new List<BigInteger>( m_store.Keys );

        public Boolean TryGetValue( KademliaId key, out String val )
        {
            val = null;
            Boolean ret = m_store.TryGetValue( key.Value, out StoreValue sv );

            if ( ret )
            {
                val = sv.Value;
            }

            return ret;
        }

        public Boolean Contains( KademliaId key ) => 
            m_store.ContainsKey( key.Value );

        public String Get( KademliaId key ) => 
            m_store[ key.Value ].Value;

        public String Get( BigInteger key ) => 
            m_store[ key ].Value;

        public DateTime GetTimeStamp( BigInteger key ) => 
            m_store[ key ].RepublishTimeStamp;

        public Int32 GetExpirationTimeSec( BigInteger key ) => 
            m_store[ key ].ExpirationTime;

        /// <summary>
        /// Updates the republish timestamp.
        /// </summary>
        public void Touch( BigInteger key ) => 
            m_store[ key ].RepublishTimeStamp = DateTime.UtcNow;

        public void Set( KademliaId key, String val, Int32 expirationTime )
        {
            m_store[ key.Value ] = new StoreValue() 
            { 
                Value = val, 
                ExpirationTime = expirationTime 
            };

            Touch( key.Value );
        }

        public void Remove( BigInteger key ) => 
            m_store.TryRemove( key, value: out _ );
    }
}
