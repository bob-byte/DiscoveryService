using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using LUC.Interfaces.Extensions;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Constants;

namespace LUC.DiscoveryServices.Kademlia.Routers
{
    class ParallelRouter : BaseRouter
    {
        // ======================
        // For parallel querying:
        protected ConcurrentQueue<ContactQueueItem> m_contactQueue;
        protected Semaphore m_semaphore;
        protected List<Thread> m_threads;

        protected Boolean m_stopWork;

        protected DateTime m_now;
        // ======================

        //The DHT sets the node.
        public ParallelRouter( UInt16 protocolVersion )
            : base( protocolVersion )
        {
            m_contactQueue = new ConcurrentQueue<ContactQueueItem>();
            m_semaphore = new Semaphore( 0, Int32.MaxValue );
            InitializeThreadPool();
        }

        public ParallelRouter( Node node, UInt16 protocolVersion )
            : base( protocolVersion )
        {
            Node = node;
            m_contactQueue = new ConcurrentQueue<ContactQueueItem>();
            m_semaphore = new Semaphore( 0, Int32.MaxValue );
            InitializeThreadPool();
        }

        /// <summary>
        /// Perform a lookup on the given key, finding either a node containing the key value
        /// or returning all closer contacts.
        /// This method is not re-entrant!  Do not call this method in parallel!
        /// </summary>
        public override (Boolean found, List<IContact> contacts, IContact foundBy, String val) Lookup(
            KademliaId key,
            Func<KademliaId, IContact, (List<IContact> contacts, IContact foundBy, String val)> rpcCall,
            Boolean giveMeAll = false )
        {
            m_stopWork = false;
            Boolean haveWork = true;
            var findResult = new FindResult();
            var ret = new List<IContact>();
            var contactedNodes = new List<IContact>();
            var closerContacts = new List<IContact>();
            var fartherContacts = new List<IContact>();
            (Boolean found, List<IContact> contacts, IContact foundBy, String val) foundReturn = (false, null, null, String.Empty);

#if TRY_CLOSEST_BUCKET
            // Spec: The lookup initiator starts by picking a nodes from its closest non-empty k-bucket
            KBucket bucket = FindClosestNonEmptyKBucket(key);

            // Not in spec -- sort by the closest nodes in the closest bucket.
            List<IContact> allNodes = node.BucketList.GetCloseContacts(key, node.OurContact.ID).Take(Constants.K).ToList(); 
            List<IContact> nodesToQuery = allNodes.Take(Constants.ALPHA).ToList();
            fartherContacts.AddRange(allNodes.Skip(Constants.ALPHA).Take(Constants.K - Constants.ALPHA));
#else
#if TRACE
            List<IContact> allNodes;
            lock (Node.BucketList)
            {
                allNodes = Node.BucketList.GetKBucket(key).Contacts.Take(DsConstants.K).ToList();
            }

#else
            // For unit testing, this is a bad way to get a list of close contacts with virtual nodes because we're always going to get the closest nodes right at the get go.
            List<IContact> allNodes = node.BucketList.GetCloseContacts(key, node.OurContact.ID).Take(Constants.K).ToList(); 
#endif
            var nodesToQuery = allNodes.Take( DsConstants.ALPHA ).ToList();

            // Also not explicitly in spec:
            // Any closer node in the alpha list is immediately added to our closer contact list, and
            // any farther node in the alpha list is immediately added to our farther contact list.
            closerContacts.AddRange( nodesToQuery.Where( n => ( n.KadId ^ key ) < ( Node.OurContact.KadId ^ key ) ) );
            fartherContacts.AddRange( nodesToQuery.Where( n => ( n.KadId ^ key ) >= ( Node.OurContact.KadId ^ key ) ) );

            // The remaining contacts not tested yet can be put here.
            fartherContacts.AddRange( allNodes.Skip( DsConstants.ALPHA ).Take( DsConstants.K - DsConstants.ALPHA ) );
#endif

            // We're about to contact these nodes.
            contactedNodes.AddRangeDistinctBy( nodesToQuery, ( a, b ) => a.KadId == b.KadId );

            // Spec: The initiator then sends parallel, asynchronous FIND_NODE RPCS to the a nodes it has chosen, 
            // a is a system-wide concurrency parameter, such as 3.

            nodesToQuery.ForEach( n => QueueWork( key, n, rpcCall, closerContacts, fartherContacts, findResult ) );
            SetQueryTime();

            // Add any new closer contacts to the list we're going to return.
            ret.AddRangeDistinctBy( closerContacts, ( a, b ) => a.KadId == b.KadId );

            // Spec: The lookup terminates when the initiator has queried and gotten responses from the k closest nodes it has seen.
            while ( ret.Count < DsConstants.K && haveWork )
            {
                Thread.Sleep( DsConstants.RESPONSE_WAIT_TIME );

                if ( ParallelFound( findResult, ref foundReturn ) )
                {
#if DEBUG       // For unit testing.
                    CloserContacts = closerContacts;
                    FartherContacts = fartherContacts;
#endif
                    StopRemainingWork();

                    return foundReturn;
                }

                var closerUncontactedNodes = closerContacts.Except( contactedNodes ).ToList();
                var fartherUncontactedNodes = fartherContacts.Except( contactedNodes ).ToList();
                Boolean haveCloser = closerUncontactedNodes.Count > 0;
                Boolean haveFarther = fartherUncontactedNodes.Count > 0;

                haveWork = haveCloser || haveFarther || !QueryTimeExpired();

                // Spec:  Of the k nodes the initiator has heard of closest to the target...
                if ( haveCloser )
                {
                    // We're about to contact these nodes.
                    IEnumerable<IContact> alphaNodes = closerUncontactedNodes.Take( DsConstants.ALPHA );
                    contactedNodes.AddRangeDistinctBy( alphaNodes, ( a, b ) => a.KadId == b.KadId );
                    alphaNodes.ForEach( n => QueueWork( key, n, rpcCall, closerContacts, fartherContacts, findResult ) );
                    SetQueryTime();
                }
                else if ( haveFarther )
                {
                    // We're about to contact these nodes.
                    IEnumerable<IContact> alphaNodes = fartherUncontactedNodes.Take( DsConstants.ALPHA );
                    contactedNodes.AddRangeDistinctBy( alphaNodes, ( a, b ) => a.KadId == b.KadId );
                    alphaNodes.ForEach( n => QueueWork( key, n, rpcCall, closerContacts, fartherContacts, findResult ) );
                    SetQueryTime();
                }
            }

#if DEBUG       // For unit testing.
            CloserContacts = closerContacts;
            FartherContacts = fartherContacts;
#endif

            StopRemainingWork();

            // Spec (sort of): Return max(k) closer nodes, sorted by distance.
            // For unit testing, giveMeAll can be true so that we can match against our alternate way of getting closer contacts.
            lock ( m_locker )
            {
                // Clone the returning closer contact list so any threads still to return don't affect the collection at this point.
                return (false, new List<IContact>( giveMeAll ? ret : ret.Take( DsConstants.K ).OrderBy( c => c.KadId ^ key ).ToList() ), null, null);
            }
        }

        /// <summary>
        /// Sets the time of the query to now.
        /// </summary>
        protected void SetQueryTime() => m_now = DateTime.UtcNow;

        /// <summary>
        /// Returns true if the query time has expired.
        /// </summary>
        protected Boolean QueryTimeExpired() => ( DateTime.UtcNow - m_now ).TotalMilliseconds > DsConstants.QUERY_TIME;

        protected void InitializeThreadPool()
        {
            m_threads = new List<Thread>();
            DsConstants.MAX_THREADS.ForEach( () =>
             {
                 var thread = new Thread( new ThreadStart( RpcCaller ) )
                 {
                     IsBackground = true
                 };
                 thread.Start();
             } );
        }

#pragma warning disable S2190 // Recursion should not be infinite
        private void RpcCaller()
#pragma warning restore S2190 // Recursion should not be infinite
        {
            while ( true )
            {
                m_semaphore.WaitOne();

                if ( !m_contactQueue.TryDequeue( out ContactQueueItem item ) )
                {
                    continue;
                }

                if ( !GetCloserNodes(
                        item.Key,
                        item.IContact,
                        item.RpcCall,
                        item.CloserContacts,
                        item.FartherContacts,
                        out String val,
                        out IContact foundBy ) )
                {
                    continue;
                }

                if ( m_stopWork )
                {
                    continue;
                }

                // Possible multiple "found"
                lock ( m_locker )
                {
                    item.FindResult.Found = true;
                    item.FindResult.FoundBy = foundBy;
                    item.FindResult.FoundValue = val;
                    item.FindResult.FoundContacts = new List<IContact>( item.CloserContacts );
                }
            }
        }

        protected void QueueWork(
            KademliaId key,
            IContact contact,
            Func<KademliaId, IContact, (List<IContact> contacts, IContact foundBy, String val)> rpcCall,
            List<IContact> closerContacts,
            List<IContact> fartherContacts,
            FindResult findResult
            )
        {
            m_contactQueue.Enqueue( new ContactQueueItem()
            {
                Key = key,
                IContact = contact,
                RpcCall = rpcCall,
                CloserContacts = closerContacts,
                FartherContacts = fartherContacts,
                FindResult = findResult
            } );

            m_semaphore.Release();
        }

        private void StopRemainingWork()
        {
            DequeueRemainingWork();
            m_stopWork = true;
        }

        private void DequeueRemainingWork()
        {
            while ( m_contactQueue.TryDequeue( out _ ) )
            { };
        }

        private Boolean ParallelFound( FindResult findResult, ref (Boolean found, List<IContact> contacts, IContact foundBy, String val) foundRet )
        {
            lock ( m_locker )
            {
                if ( findResult.Found )
                {
                    // Prevent found contacts from changing as we clone the found contacts list.
                    lock ( findResult.FoundContacts )
                    {
                        foundRet = (true, findResult.FoundContacts.ToList(), findResult.FoundBy, findResult.FoundValue);
                    }
                }

                return findResult.Found;
            }
        }
    }
}
