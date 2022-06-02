using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Timers;

using Newtonsoft.Json;

using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Kademlia.Interfaces;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Constants;
using LUC.DiscoveryServices.Common;

namespace LUC.DiscoveryServices.Kademlia
{
    /// <summary>
    /// DHT - distributed hash table. It minimize settings count of messages, which <seealso cref="IContact"/>s should send to learn each other 
    /// </summary>
    class Dht : AbstractKademlia, IDht
    {
        protected List<IContact> m_pendingContacts;

        protected Timer m_bucketRefreshTimer;
        protected Timer m_keyValueRepublishTimer;
        protected Timer m_originatorRepublishTimer;
        protected Timer m_expireKeysTimer;

        protected UInt16 m_protocolVersion;

        //// For serializer, empty constructor needed.
        public Dht()
            : base(protocolVersion: GeneralConstants.PROTOCOL_VERSION)
        {
            m_protocolVersion = GeneralConstants.PROTOCOL_VERSION;
        }

        /// <summary>
        /// Use this constructor to initialize the stores to the same instance.
        /// </summary>
        public Dht(IContact contact, UInt16 protocolVersion, Func<IStorage> storageFactory)
            : this(contact, protocolVersion, storageFactory(), storageFactory(), storageFactory())
        {
            ;//do nothing
        }

        /// <summary>
        /// Supports different concrete storage types.  For example, you may want the cacheStorage
        /// to be an in memory store, the originatorStorage to be a SQL database, and the republish store
        /// to be a key-value database.
        /// </summary>
        public Dht(IContact contact, UInt16 protocolVersion, IStorage originatorStorage, IStorage republishStorage, IStorage cacheStorage)
            : base(protocolVersion)
        {
            OriginatorStorage = originatorStorage;
            RepublishStorage = republishStorage;
            CacheStorage = cacheStorage;
            m_protocolVersion = protocolVersion;

            FinishInitialization(contact);
            SetupTimers();
        }

        [JsonIgnore]
        public ConcurrentDictionary<BigInteger, Int32> EvictionCount { get; private set; }

        [JsonIgnore]
        public List<IContact> PendingContacts => m_pendingContacts;

        /// <summary>
        /// Current Peer
        /// </summary>
        [JsonIgnore]
        public Node Node { get; set; }

        [JsonIgnore]
        public IStorage CacheStorage { get; set; }

        [JsonIgnore]
        public KademliaId ID { get; protected set; }

        [JsonIgnore]
        public Int32 PendingPeersCount
        {
            get
            {
                lock (m_pendingContacts)
                {
                    return m_pendingContacts.Count;
                }
            }
        }

        [JsonIgnore]
        public Int32 PendingEvictionCount => EvictionCount.Count;

        public IStorage RepublishStorage { get; set; }
        public IStorage OriginatorStorage { get; set; }

        /// <summary>
        /// IP-address TCP port and ID where we listen and send messages 
        /// </summary>
        public IContact OurContact { get; set; }

        public List<IContact> OnlineContacts
        {
            get
            {
                lock ( Node.BucketList )
                {
                    var onlineContacts = Node.BucketList.Buckets.SelectMany(c => c.Contacts).ToList();
                    onlineContacts.AddRange(PendingContacts);

                    return onlineContacts;
                }
            }
        }

        /// <summary>
        /// Returns a JSON string of the serialized DHT.
        /// </summary>
        public String Save()
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            };
            String json = JsonConvert.SerializeObject(this, Formatting.Indented, settings);

            return json;
        }

        public static Dht Load(String json)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            };

            Dht dht = JsonConvert.DeserializeObject<Dht>(json, settings);
            dht.DeserializationFixups();
            dht.SetupTimers();

            return dht;
        }

        protected void DeserializationFixups()
        {
            ID = OurContact.KadId;
            Node.OurContact = OurContact;
            Node.BucketList.OurID = ID;
            Node.BucketList.OurContact = OurContact;
            Node.Dht = this;
        }

        /// <summary>
        /// Bootstrap our peer by contacting another peer, adding its contacts
        /// to our list, then refresh all buckets of the <see cref="OurContact"/>, 
        /// except bucket of the <paramref name="knownPeer"/> not to include additional contact
        /// </summary>
        public RpcError Bootstrap(IContact knownPeer)
        {
            //to don't have circle message exchange during multicast sendings
#if RECEIVE_UDP_FROM_OURSELF
            Node.BucketList.AddContact( knownPeer );
#endif

            (List<IContact> contacts, RpcError error) = m_remoteProcedureCaller.FindNode(OurContact, OurContact.KadId, knownPeer);

            if ( !error.HasError )
            {
#if !RECEIVE_UDP_FROM_OURSELF
                Node.BucketList.AddContact( knownPeer );
#endif

                contacts.ForEach(c => Node.BucketList.AddContact(c));

                KBucket knownPeerBucket = Node.BucketList.GetKBucket(knownPeer.KadId);

                DateTime now = DateTime.UtcNow;

                // Resolve the list now, so we don't include additional contacts as we add to our bucket additional contacts.
                var otherBuckets = Node.BucketList.Buckets.Where(b => b != knownPeerBucket).Where( b => ( now - b.TimeStamp ).TotalMilliseconds >= DsConstants.BUCKET_REFRESH_INTERVAL ).ToList();
                otherBuckets.ForEach(b => RefreshBucket(b));
            }
            else
            {
                HandleError(error, knownPeer);
            }

            return error;
        }

        public void Store(KademliaId key, String val)
        {
            TouchBucketWithKey(key);

            // We're storing to k closer contacts.
            OriginatorStorage.Set(key, val);
            StoreOnCloserContacts(key, val);
        }

        public void FindValue(KademliaId key, out Boolean found, out List<IContact> closeContacts, out String nodeValue)
        {
            TouchBucketWithKey(key);

            closeContacts = null;

            found = OriginatorStorage.TryGetValue( key, out nodeValue ) ||
                  RepublishStorage.TryGetValue( key, out nodeValue ) ||
                  CacheStorage.TryGetValue( key, out nodeValue );
        }

#if DEBUG       // For demo and unit testing.
        public void PerformBucketRefresh()
        {
            // Get current bucket list in a separate collection because the bucket list might be modified
            // as the result of a bucket split.
            var currentBuckets = new List<KBucket>( Node.BucketList.Buckets );
            currentBuckets.ForEach( b => RefreshBucket( b ) );
        }

        public void PerformStoreRepublish() => RepublishStorage.Keys.ForEach( k =>
                                             {
                                                 var key = new KademliaId( k );
                                                 StoreOnCloserContacts( key, RepublishStorage.Get( key ) );
                                                 RepublishStorage.Touch( k );
                                             } );
#endif

        /// <summary>
        /// Put the timed out contact into a collection and increment the number of times it has timed out.
        /// If it has timed out a certain amount, remove it from the bucket and replace it with the most
        /// recent pending contact that are queued for that bucket.
        /// </summary>
        public void HandleError(RpcError error, IContact contact)
        {
            if ( error.HasError )
            {
                // For all errors:
                Int32 count = AddContactToEvict(contact.KadId.Value);

                if ( count == DsConstants.EVICTION_LIMIT )
                {
                    ReplaceContact(contact);
                }
            }
        }

        public void AddToPending(IContact pending)
        {
            lock ( m_pendingContacts )
            {
                m_pendingContacts.AddDistinctBy(pending, c => c.KadId);
            }
        }

        /// <summary>
        ///  The contact that did not respond (or had an error) gets n tries before being evicted
        ///  and replaced with the most recently contact that wants to go into the non-responding contact's kbucket.
        /// </summary>
        /// <param name="toEvict">The contact that didn't respond.</param>
        /// <param name="toReplace">The contact that can replace the non-responding contact.</param>
        public void DelayEviction(IContact toEvict, IContact toReplace)
        {
            if ( toReplace != null )
            {
                // Non-concurrent list needs locking.
                lock ( m_pendingContacts )
                {
                    // Add only if it's a new pending contact.
                    m_pendingContacts.AddDistinctBy(toReplace, c => c.KadId);
                }
            }

            BigInteger key = toEvict.KadId.Value;
            Int32 count = AddContactToEvict(key);

            if ( count == DsConstants.EVICTION_LIMIT )
            {
                ReplaceContact(toEvict);
            }
        }

        protected Int32 AddContactToEvict(BigInteger key)
        {
            if ( !EvictionCount.ContainsKey( key ) )
            {
                EvictionCount[ key ] = 0;
            }

            Int32 count = EvictionCount[ key ] + 1;
            EvictionCount[ key ] = count;

            return count;
        }

        protected void ReplaceContact(IContact toEvict)
        {
            try
            {
                KBucket bucket = Node.BucketList.GetKBucket(toEvict.KadId);

                // Prevent other threads from manipulating the bucket list or buckets.
                lock (Node.BucketList)
                {
                    EvictContact(bucket, toEvict);
                    ReplaceWithPendingContact(bucket);
                }
            }
            catch ( BucketDoesNotContainContactToEvictException )
            {
                ;//do nothing
            }
            //bucket wasn't found
            catch ( IndexOutOfRangeException )
            {
                ;//do nothing 
            }
        }

        protected void EvictContact(KBucket bucket, IContact toEvict)
        {
            _ = EvictionCount.TryRemove( toEvict.KadId.Value, out _ );
            if ( bucket.Contains( toEvict ) )
            {
                bucket.EvictContact( toEvict );
            }
            else
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( exception: new BucketDoesNotContainContactToEvictException( msg: "Bucket doesn't contain the contact to be evicted." ) );
            }
        }

        /// <summary>
        /// Find a pending contact that goes into the bucket that now has room.
        /// </summary>
        protected void ReplaceWithPendingContact(KBucket bucket)
        {
            IContact contact;

            // Non-concurrent list needs locking while we query it.
            lock ( m_pendingContacts )
            {
                contact = m_pendingContacts.Where(c => Node.BucketList.GetKBucket(c.KadId) == bucket).OrderBy(c => c.LastSeen).LastOrDefault();

                if ( contact != null )
                {
                    m_pendingContacts.Remove(contact);
                    bucket.AddContact(contact);
                }
            }
        }

        /// <summary>
        /// Return the number of nodes between the two contacts, where the contact list is sorted by the integer ID values (not XOR distance.)
        /// </summary>
        protected Int32 GetSeparatingNodesCount(IContact a, IContact b)
        {
            List<IContact> allContacts;
            lock (Node.BucketList)
            {
                // Sort of brutish way to do this.
                // Get all the contacts, ordered by their ID.
                allContacts = Node.BucketList.Buckets.SelectMany(c => c.Contacts).OrderBy(c => c.KadId.Value).ToList();
            }

            Int32 idxa = allContacts.IndexOf(a);
            Int32 idxb = allContacts.IndexOf(b);

            return Math.Abs(idxa - idxb);
        }

        public void FinishLoad()
        {
            EvictionCount = new ConcurrentDictionary<BigInteger, Int32>();
            m_pendingContacts = new List<IContact>();
            Node.Storage = RepublishStorage;
            Node.CacheStorage = CacheStorage;
            Node.Dht = this;
            Node.BucketList.Dht = this;
            SetupTimers();
        }

        protected void FinishInitialization(IContact contact)
        {
            EvictionCount = new ConcurrentDictionary<BigInteger, Int32>();
            m_pendingContacts = new List<IContact>();
            ID = contact.KadId;
            OurContact = contact;
            Node = new Node(OurContact, m_protocolVersion, RepublishStorage, CacheStorage)
            {
                Dht = this
            };
            Node.BucketList.Dht = this;
        }

        protected void SetupTimers()
        {
            SetupBucketRefreshTimer();
            SetupKeyValueRepublishTimer();
            SetupOriginatorRepublishTimer();
            SetupExpireKeysTimer();
        }

        protected void TouchBucketWithKey(KademliaId key) => Node.BucketList.GetKBucket(key).Touch();

        protected void SetupBucketRefreshTimer()
        {
            m_bucketRefreshTimer = new Timer(DsConstants.BUCKET_REFRESH_INTERVAL)
            {
                AutoReset = true
            };
            m_bucketRefreshTimer.Elapsed += BucketRefreshTimerElapsed;
            m_bucketRefreshTimer.Start();
        }

        protected void SetupKeyValueRepublishTimer()
        {
            m_keyValueRepublishTimer = new Timer(DsConstants.KEY_VALUE_REPUBLISH_INTERVAL)
            {
                AutoReset = true
            };
            m_keyValueRepublishTimer.Elapsed += KeyValueRepublishElapsed;
            m_keyValueRepublishTimer.Start();
        }

        protected void SetupOriginatorRepublishTimer()
        {
            m_originatorRepublishTimer = new Timer(DsConstants.ORIGINATOR_REPUBLISH_INTERVAL)
            {
                AutoReset = true
            };
            m_originatorRepublishTimer.Elapsed += OriginatorRepublishElapsed;
            m_originatorRepublishTimer.Start();
        }

        protected void SetupExpireKeysTimer()
        {
            m_expireKeysTimer = new Timer(DsConstants.KEY_VALUE_EXPIRE_INTERVAL)
            {
                AutoReset = true
            };
            m_expireKeysTimer.Elapsed += ExpireKeysElapsed;
            m_expireKeysTimer.Start();
        }

        protected void BucketRefreshTimerElapsed(Object sender, ElapsedEventArgs e)
        {
            List<KBucket> currentBuckets;
            DateTime now = DateTime.UtcNow;

            lock ( Node.BucketList )
            {
                // Put into a separate list as bucket collections may be modified.
                currentBuckets = new List<KBucket>(Node.BucketList.Buckets.
                    Where(b => (now - b.TimeStamp).TotalMilliseconds >= DsConstants.BUCKET_REFRESH_INTERVAL));
            }

            currentBuckets.ForEach( b => RefreshBucket( b ) );
        }

        /// <summary>
        /// Replicate key values if the key-value hasn't been touched within the republish interval.
        /// Also don't do a FindNode lookup if the bucket containing the key has been refreshed within the refresh interval.
        /// </summary>
        protected void KeyValueRepublishElapsed(Object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.UtcNow;

            RepublishStorage.Keys.Where( k => ( now - RepublishStorage.GetTimeStamp( k ) ).TotalMilliseconds >= DsConstants.KEY_VALUE_REPUBLISH_INTERVAL ).ForEach( k =>
                   {
                       var key = new KademliaId( k );
                       StoreOnCloserContacts( key, RepublishStorage.Get( key ) );
                       RepublishStorage.Touch( k );
                   } );
        }

        protected void OriginatorRepublishElapsed(Object sender, ElapsedEventArgs e)
        {
            DateTime now = DateTime.UtcNow;

            OriginatorStorage.Keys.Where( k => ( now - OriginatorStorage.GetTimeStamp( k ) ).
                TotalMilliseconds >= DsConstants.ORIGINATOR_REPUBLISH_INTERVAL ).
                ForEach( k =>
                {
                    var key = new KademliaId(k);
                    // Just use close contacts, don't do a lookup.
                    List<IContact> contacts = Node.BucketList.GetCloseContacts(key, Node.OurContact.MachineId);

                    contacts.ForEach(c =>
                    {
                        RpcError error = m_remoteProcedureCaller.Store(OurContact, key, OriginatorStorage.Get(key), c);
                        HandleError(error, c);
                    });

                    OriginatorStorage.Touch(k);
                });
        }

        /// <summary>
        /// Any expired keys in the republish or node's cache are removed.
        /// </summary>
        protected virtual void ExpireKeysElapsed(Object sender, ElapsedEventArgs e)
        {
            RemoveExpiredData(CacheStorage);
            RemoveExpiredData(RepublishStorage);
        }

        protected void RemoveExpiredData(IStorage store)
        {
            DateTime now = DateTime.UtcNow;
            // ToList so our key list is resolved now as we remove keys.
            store.Keys.Where( k => ( now - store.GetTimeStamp( k ) ).TotalSeconds >= store.GetExpirationTimeSec( k ) ).ToList().ForEach( k => store.Remove( k ) );
        }

        /// <summary>
        /// Perform a lookup if the bucket containing the key has not been refreshed, 
        /// otherwise just get the contacts the k closest contacts we know about.
        /// </summary>
        protected void StoreOnCloserContacts(KademliaId key, String val)
        {
            List<IContact> closerContacts = Node.BucketList.GetCloseContacts( key, Node.OurContact.MachineId );

            closerContacts.ForEach(closerContact =>
            {
                RpcError error = m_remoteProcedureCaller.Store(Node.OurContact, key, val, closerContact);
                HandleError(error, closerContact);
            });
        }

        protected void RefreshBucket(KBucket bucket)
        {
            bucket.Touch();

            KademliaId rndId = bucket.RandomIDWithinBucket();

            List<IContact> contacts;
            lock (Node.BucketList)
            {
                // Isolate in a separate list as contacts collection for this bucket might change.
                contacts = bucket.Contacts.ToList();
            }

            contacts.ForEach(contact =>
            {
                (List<IContact> newContacts, RpcError timeoutError) = m_remoteProcedureCaller.FindNode(OurContact, rndId, contact);
                HandleError(timeoutError, contact);

                newContacts?.ForEach(otherContact => Node.BucketList.AddContact(otherContact));
            });
        }
    }
}
