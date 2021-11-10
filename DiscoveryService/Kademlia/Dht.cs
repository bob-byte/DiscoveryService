using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Timers;

using Newtonsoft.Json;

using LUC.DiscoveryService.Kademlia.Routers;
using LUC.Interfaces;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Kademlia.Interfaces;
using LUC.DiscoveryService.Common;

namespace LUC.DiscoveryService.Kademlia
{
    /// <summary>
    /// DHT - distributed hash table. It minimize settings count of messages, which <seealso cref="Contact"/>s should send to learn each other 
    /// </summary>
    class Dht : AbstractKademlia, IDht
    {
        protected List<Contact> m_pendingContacts;

        protected Timer m_bucketRefreshTimer;
        protected Timer m_keyValueRepublishTimer;
        protected Timer m_originatorRepublishTimer;
        protected Timer m_expireKeysTimer;

        protected UInt16 m_protocolVersion;

        //// For serializer, empty constructor needed.
        public Dht()
            : base( protocolVersion: 1 )
        {
            ;//do nothing
        }

        /// <summary>
        /// Use this constructor to initialize the stores to the same instance.
        /// </summary>
        public Dht( Contact contact, UInt16 protocolVersion, Func<IStorage> storageFactory, BaseRouter router )
            : this( contact, router, protocolVersion, storageFactory(), storageFactory(), storageFactory() )
        {
            ;//do nothing
        }

        /// <summary>
        /// Supports different concrete storage types.  For example, you may want the cacheStorage
        /// to be an in memory store, the originatorStorage to be a SQL database, and the republish store
        /// to be a key-value database.
        /// </summary>
        public Dht( Contact contact, BaseRouter router, UInt16 protocolVersion, IStorage originatorStorage, IStorage republishStorage, IStorage cacheStorage )
            : base( protocolVersion )
        {
            OriginatorStorage = originatorStorage;
            RepublishStorage = republishStorage;
            CacheStorage = cacheStorage;
            m_protocolVersion = protocolVersion;

            FinishInitialization( contact, router );
            SetupTimers();
        }


        public BaseRouter Router { get; set; }

        [JsonIgnore]
        public ConcurrentDictionary<BigInteger, Int32> EvictionCount { get; private set; }

        [JsonIgnore]
        public List<Contact> PendingContacts => m_pendingContacts;

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
        public Int32 PendingPeersCount { get { lock ( m_pendingContacts ) { return m_pendingContacts.Count; } } }

        [JsonIgnore]
        public Int32 PendingEvictionCount => EvictionCount.Count;

        public IStorage RepublishStorage { get; set; }
        public IStorage OriginatorStorage { get; set; }

        /// <summary>
        /// IP-address TCP port and ID where we listen and send messages 
        /// </summary>
        public Contact OurContact { get; set; }

        public List<Contact> OnlineContacts
        {
            get
            {
                lock ( Node.BucketList )
                {
                    List<Contact> onlineContacts = Node.BucketList.Buckets.SelectMany( c => c.Contacts ).ToList();

                    return onlineContacts;
                }
            }
        }

                /// <summary>
                /// Returns a JSON string of the serialized DHT.
                /// </summary>
                public String Save()
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;
            String json = JsonConvert.SerializeObject( this, Formatting.Indented, settings );

            return json;
        }

        public static Dht Load( String json )
        {
            JsonSerializerSettings settings = new JsonSerializerSettings();
            settings.TypeNameHandling = TypeNameHandling.Auto;

            Dht dht = JsonConvert.DeserializeObject<Dht>( json, settings );
            dht.DeserializationFixups();
            dht.SetupTimers();

            return dht;
        }

        protected void DeserializationFixups()
        {
            ID = OurContact.KadId;
            Node = Router.Node;
            Node.OurContact = OurContact;
            Node.BucketList.OurID = ID;
            Node.BucketList.OurContact = OurContact;
            Router.Dht = this;
            Node.Dht = this;
        }

        /// <summary>
        /// Bootstrap our peer by contacting another peer, adding its contacts
        /// to our list, then refresh all buckets of the <see cref="OurContact"/>, 
        /// except bucket of the <paramref name="knownPeer"/> not to include additional contact
        /// </summary>
        public RpcError Bootstrap( Contact knownPeer )
        {
            Node.BucketList.AddContact( knownPeer );
            (List<Contact> contacts, RpcError error) = m_clientKadOperation.FindNode( OurContact, OurContact.KadId, knownPeer );

            if ( !error.HasError )
            {
                contacts.ForEach( c => Node.BucketList.AddContact( c ) );

                KBucket knownPeerBucket = Node.BucketList.GetKBucket( knownPeer.KadId );
                // Resolve the list now, so we don't include additional contacts as we add to our bucket additional contacts.
                List<KBucket> otherBuckets = Node.BucketList.Buckets.Where( b => b != knownPeerBucket ).ToList();
                otherBuckets.ForEach( b => RefreshBucket( b ) );
            }
            else
            {
                HandleError( error, knownPeer );
            }

            return error;
        }

        public void Store( KademliaId key, String val )
        {
            TouchBucketWithKey( key );

            // We're storing to k closer contacts.
            OriginatorStorage.Set( key, val );
            StoreOnCloserContacts( key, val );
        }

        public void FindValue( KademliaId key, out Boolean found, out List<Contact> closeContacts, out String nodeValue )
        {
            TouchBucketWithKey( key );

            List<Contact> contactsQueried = new List<Contact>();

            closeContacts = null;
            nodeValue = null;

            if ( ( OriginatorStorage.TryGetValue( key, out nodeValue ) ) ||
                 ( RepublishStorage.TryGetValue( key, out nodeValue ) ) ||
                 ( CacheStorage.TryGetValue( key, out nodeValue ) ) )
            {
                found = true;
            }
            else
            {
                (Boolean found, List<Contact> contacts, Contact foundBy, String val) lookup = Router.Lookup( key, Router.RpcFindValue );
                found = lookup.found;

                if ( lookup.found )
                {
                    nodeValue = lookup.val;
                    closeContacts = lookup.contacts;
                    // Find the first close contact (other than the one the value was found by) in which to *cache* the key-value.
                    Contact storeTo = lookup.contacts.Where( c => c != lookup.foundBy ).OrderBy( c => c.KadId ^ key ).FirstOrDefault();

                    if ( storeTo != null )
                    {
                        Int32 separatingNodes = GetSeparatingNodesCount( OurContact, storeTo );
                        Int32 expTimeSec = (Int32)( Constants.EXPIRATION_TIME_SECONDS / Math.Pow( 2, separatingNodes ) );
                        RpcError error = m_clientKadOperation.Store( Node.OurContact, key, lookup.val, storeTo, true, expTimeSec );
                        HandleError( error, storeTo );
                    }
                }
            }
        }

        public void UpdateSenderByMachineId( String machineId )
        {
            Contact sender = OnlineContacts.SingleOrDefault( c => c.MachineId == machineId );

            UpdateSender( sender );
        }

        public void UpdateSender(Contact sender)
        {
            if ( sender != null )
            {
                Node.BucketList.AddContact( sender );
            }
        }
        
#if DEBUG       // For demo and unit testing.
        public void PerformBucketRefresh()
        {
            // Get current bucket list in a separate collection because the bucket list might be modified
            // as the result of a bucket split.
            List<KBucket> currentBuckets = new List<KBucket>( Node.BucketList.Buckets );
            currentBuckets.ForEach( b => RefreshBucket( b ) );
        }

        public void PerformStoreRepublish()
        {
            RepublishStorage.Keys.ForEach( k =>
            {
                KademliaId key = new KademliaId( k );
                StoreOnCloserContacts( key, RepublishStorage.Get( key ) );
                RepublishStorage.Touch( k );
            } );
        }
#endif

        /// <summary>
        /// Put the timed out contact into a collection and increment the number of times it has timed out.
        /// If it has timed out a certain amount, remove it from the bucket and replace it with the most
        /// recent pending contact that are queued for that bucket.
        /// </summary>
        public void HandleError( RpcError error, Contact contact )
        {
            // For all errors:
            Int32 count = AddContactToEvict( contact.KadId.Value );

            if ( count == Constants.EVICTION_LIMIT )
            {
                ReplaceContact( contact );
            }
        }

        public void AddToPending( Contact pending )
        {
            lock ( m_pendingContacts )
            {
                m_pendingContacts.AddDistinctBy( pending, c => c.KadId );
            }
        }

        /// <summary>
        ///  The contact that did not respond (or had an error) gets n tries before being evicted
        ///  and replaced with the most recently contact that wants to go into the non-responding contact's kbucket.
        /// </summary>
        /// <param name="toEvict">The contact that didn't respond.</param>
        /// <param name="toReplace">The contact that can replace the non-responding contact.</param>
        public void DelayEviction( Contact toEvict, Contact toReplace )
        {
            if ( toReplace != null )
            {
                // Non-concurrent list needs locking.
                lock ( m_pendingContacts )
                {
                    // Add only if it's a new pending contact.
                    m_pendingContacts.AddDistinctBy( toReplace, c => c.KadId );
                }
            }

            BigInteger key = toEvict.KadId.Value;
            Int32 count = AddContactToEvict( key );

            if ( count == Constants.EVICTION_LIMIT )
            {
                ReplaceContact( toEvict );
            }
        }

        protected Int32 AddContactToEvict( BigInteger key )
        {
            if ( !EvictionCount.ContainsKey( key ) )
            {
                EvictionCount[ key ] = 0;
            }

            Int32 count = EvictionCount[ key ] + 1;
            EvictionCount[ key ] = count;

            return count;
        }

        protected void ReplaceContact( Contact toEvict )
        {
            KBucket bucket = Node.BucketList.GetKBucket( toEvict.KadId );

            // Prevent other threads from manipulating the bucket list or buckets.
            lock ( Node.BucketList )
            {
                EvictContact( bucket, toEvict );
                ReplaceWithPendingContact( bucket );
            }
        }

        protected void EvictContact( KBucket bucket, Contact toEvict )
        {
            _ = EvictionCount.TryRemove( toEvict.KadId.Value, out _ );
            Validate.IsTrue<BucketDoesNotContainContactToEvict>( bucket.Contains( toEvict.MachineId ), "Bucket doesn't contain the contact to be evicted." );
            bucket.EvictContact( toEvict );
        }

        /// <summary>
        /// Find a pending contact that goes into the bucket that now has room.
        /// </summary>
        protected void ReplaceWithPendingContact( KBucket bucket )
        {
            Contact contact;

            // Non-concurrent list needs locking while we query it.
            lock ( m_pendingContacts )
            {
                contact = m_pendingContacts.Where( c => Node.BucketList.GetKBucket( c.KadId ) == bucket ).OrderBy( c => c.LastSeen ).LastOrDefault();

                if ( contact != null )
                {
                    m_pendingContacts.Remove( contact );
                    bucket.AddContact( contact );
                }
            }
        }

        /// <summary>
        /// Return the number of nodes between the two contacts, where the contact list is sorted by the integer ID values (not XOR distance.)
        /// </summary>
        protected Int32 GetSeparatingNodesCount( Contact a, Contact b )
        {
            // Sort of brutish way to do this.
            // Get all the contacts, ordered by their ID.
            List<Contact> allContacts = Node.BucketList.Buckets.SelectMany( c => c.Contacts ).OrderBy( c => c.KadId.Value ).ToList();

            Int32 idxa = allContacts.IndexOf( a );
            Int32 idxb = allContacts.IndexOf( b );

            return Math.Abs( idxa - idxb );
        }

        public void FinishLoad()
        {
            EvictionCount = new ConcurrentDictionary<BigInteger, Int32>();
            m_pendingContacts = new List<Contact>();
            Node.Storage = RepublishStorage;
            Node.CacheStorage = CacheStorage;
            Node.Dht = this;
            Node.BucketList.Dht = this;
            Router.Node = Node;
            Router.Dht = this;
            SetupTimers();
        }

        protected void FinishInitialization( Contact contact, BaseRouter router )
        {
            EvictionCount = new ConcurrentDictionary<BigInteger, Int32>();
            m_pendingContacts = new List<Contact>();
            ID = contact.KadId;
            OurContact = contact;
            Node = new Node( OurContact, m_protocolVersion, RepublishStorage, CacheStorage )
            {
                Dht = this
            };
            Node.BucketList.Dht = this;
            Router = router;
            Router.Node = Node;
            Router.Dht = this;
        }

        protected void SetupTimers()
        {
            SetupBucketRefreshTimer();
            SetupKeyValueRepublishTimer();
            SetupOriginatorRepublishTimer();
            SetupExpireKeysTimer();
        }

        protected void TouchBucketWithKey( KademliaId key ) => Node.BucketList.GetKBucket( key ).Touch();

        protected void SetupBucketRefreshTimer()
        {
            m_bucketRefreshTimer = new Timer( Constants.BUCKET_REFRESH_INTERVAL );
            m_bucketRefreshTimer.AutoReset = true;
            m_bucketRefreshTimer.Elapsed += BucketRefreshTimerElapsed;
            m_bucketRefreshTimer.Start();
        }

        protected void SetupKeyValueRepublishTimer()
        {
            m_keyValueRepublishTimer = new Timer( Constants.KEY_VALUE_REPUBLISH_INTERVAL );
            m_keyValueRepublishTimer.AutoReset = true;
            m_keyValueRepublishTimer.Elapsed += KeyValueRepublishElapsed;
            m_keyValueRepublishTimer.Start();
        }

        protected void SetupOriginatorRepublishTimer()
        {
            m_originatorRepublishTimer = new Timer( Constants.ORIGINATOR_REPUBLISH_INTERVAL );
            m_originatorRepublishTimer.AutoReset = true;
            m_originatorRepublishTimer.Elapsed += OriginatorRepublishElapsed;
            m_originatorRepublishTimer.Start();
        }

        protected void SetupExpireKeysTimer()
        {
            m_expireKeysTimer = new Timer( Constants.KEY_VALUE_EXPIRE_INTERVAL );
            m_expireKeysTimer.AutoReset = true;
            m_expireKeysTimer.Elapsed += ExpireKeysElapsed;
            m_expireKeysTimer.Start();
        }

        protected void BucketRefreshTimerElapsed( Object sender, ElapsedEventArgs e )
        {
            lock(m_bucketRefreshTimer)
            {
                DateTime now = DateTime.UtcNow;

                // Put into a separate list as bucket collections may be modified.
                List<KBucket> currentBuckets = new List<KBucket>( Node.BucketList.Buckets.
                    Where( b => ( now - b.TimeStamp ).TotalMilliseconds >= Constants.BUCKET_REFRESH_INTERVAL ) );

                currentBuckets.ForEach( b => RefreshBucket( b ) );
            }
        }

        /// <summary>
        /// Replicate key values if the key-value hasn't been touched within the republish interval.
        /// Also don't do a FindNode lookup if the bucket containing the key has been refreshed within the refresh interval.
        /// </summary>
        protected void KeyValueRepublishElapsed( Object sender, ElapsedEventArgs e )
        {
            DateTime now = DateTime.UtcNow;

            RepublishStorage.Keys.Where( k => ( now - RepublishStorage.GetTimeStamp( k ) ).TotalMilliseconds >= Constants.KEY_VALUE_REPUBLISH_INTERVAL ).ForEach( k =>
                   {
                       KademliaId key = new KademliaId( k );
                       StoreOnCloserContacts( key, RepublishStorage.Get( key ) );
                       RepublishStorage.Touch( k );
                   } );
        }

        protected void OriginatorRepublishElapsed( Object sender, ElapsedEventArgs e )
        {
            DateTime now = DateTime.UtcNow;

            OriginatorStorage.Keys.Where( k => ( now - OriginatorStorage.GetTimeStamp( k ) ).
                TotalMilliseconds >= Constants.ORIGINATOR_REPUBLISH_INTERVAL ).
                ForEach( k =>
                {
                    KademliaId key = new KademliaId( k );
                    // Just use close contacts, don't do a lookup.
                    List<Contact> contacts = Node.BucketList.GetCloseContacts( key, Node.OurContact.KadId );

                    contacts.ForEach( c =>
                    {
                        RpcError error = m_clientKadOperation.Store( OurContact, key, OriginatorStorage.Get( key ), c );
                        HandleError( error, c );
                    } );

                    OriginatorStorage.Touch( k );
                } );
        }

        /// <summary>
        /// Any expired keys in the republish or node's cache are removed.
        /// </summary>
        protected virtual void ExpireKeysElapsed( Object sender, ElapsedEventArgs e )
        {
            RemoveExpiredData( CacheStorage );
            RemoveExpiredData( RepublishStorage );
        }

        protected void RemoveExpiredData( IStorage store )
        {
            DateTime now = DateTime.UtcNow;
            // ToList so our key list is resolved now as we remove keys.
            store.Keys.Where( k => ( now - store.GetTimeStamp( k ) ).TotalSeconds >= store.GetExpirationTimeSec( k ) ).ToList().ForEach( k =>
                     {
                         store.Remove( k );
                     } );
        }

        /// <summary>
        /// Perform a lookup if the bucket containing the key has not been refreshed, 
        /// otherwise just get the contacts the k closest contacts we know about.
        /// </summary>
        protected void StoreOnCloserContacts( KademliaId key, String val )
        {
            DateTime now = DateTime.UtcNow;

            KBucket kbucket = Node.BucketList.GetKBucket( key );
            List<Contact> closerContacts;

            if ( ( now - kbucket.TimeStamp ).TotalMilliseconds < Constants.BUCKET_REFRESH_INTERVAL )
            {
                // Bucket has been refreshed recently, so don't do a lookup as we have the k closes contacts.
                closerContacts = Node.BucketList.GetCloseContacts( key, Node.OurContact.KadId );
            }
            else
            {
                closerContacts = Router.Lookup( key, Router.RpcFindNodes ).contacts;
            }

            closerContacts.ForEach( closerContact =>
             {
                 RpcError error = m_clientKadOperation.Store( Node.OurContact, key, val, closerContact );
                 HandleError( error, closerContact );
             } );
        }

        protected void RefreshBucket( KBucket bucket )
        {
            bucket.Touch();

            KademliaId rndId = KademliaId.RandomIDWithinBucket( bucket );

            // Isolate in a separate list as contacts collection for this bucket might change.
            List<Contact> contacts = bucket.Contacts.ToList();

            contacts.ForEach( contact =>
            {
                (List<Contact> newContacts, RpcError timeoutError) = m_clientKadOperation.FindNode( OurContact, rndId, contact );
                HandleError( timeoutError, contact );

                newContacts?.ForEach( otherContact => Node.BucketList.AddContact( otherContact ) );
            } );
        }
    }
}
