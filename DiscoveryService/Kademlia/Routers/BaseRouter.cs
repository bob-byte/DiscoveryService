using System;
using System.Collections.Generic;
using System.Linq;

using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.Interfaces;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Extensions;

using Newtonsoft.Json;

namespace LUC.DiscoveryServices.Kademlia.Routers
{
    internal abstract class BaseRouter : AbstractKademlia
    {
        protected readonly Object m_locker = new Object();

        protected BaseRouter( UInt16 protocolVersion )
            : base( protocolVersion )
        {
            ;//do nothing
        }

#if DEBUG       // for unit testing
        [JsonIgnore] protected List<IContact> CloserContacts { get; set; }

        [JsonIgnore] protected List<IContact> FartherContacts { get; set; }
#endif

        public Node Node { get; set; }

        [JsonIgnore]
        public Dht Dht { get; set; }

        public abstract (Boolean found, List<IContact> contacts, IContact foundBy, String val) Lookup(
            KademliaId key,
            Func<KademliaId, IContact, (List<IContact> contacts, IContact foundBy, String val)> rpcCall,
            Boolean giveMeAll = false );

        /// <summary>
        /// Using the k-bucket's key (it's high value), find the closest 
        /// k-bucket the given key that isn't empty.
        /// </summary>
#if DEBUG           // For unit testing.
        public virtual KBucket FindClosestNonEmptyKBucket( KademliaId key )
#else
        protected virtual KBucket FindClosestNonEmptyKBucket( KademliaId key )
#endif
        {
            KBucket closest;
            lock (Node.BucketList)
            {
                closest = Node.BucketList.Buckets.Where(b => b.Contacts.Count > 0).OrderBy(b => b.Key ^ key).FirstOrDefault();
            }

            Validate.IsTrue<NoNonEmptyBucketsException>( closest != null, "No non-empty buckets exist.  You must first register a peer and add that peer to your bucketlist." );

            return closest;
        }

        /// <summary>
        /// Get sorted list of closest nodes to the given key.
        /// </summary>
#if DEBUG           // For unit testing.
        public List<IContact> GetClosestNodes( KademliaId key, KBucket bucket )
#else
        protected List<IContact> GetClosestNodes( KademliaId key, KBucket bucket)
#endif
        {
            lock (Node.BucketList)
            {
                return bucket.Contacts.OrderBy(c => c.KadId ^ key).ToList();
            }
        }

        public Boolean GetCloserNodes(
            KademliaId key,
            IContact nodeToQuery,
            Func<KademliaId, IContact, (List<IContact> contacts, IContact foundBy, String val)> rpcCall,
            List<IContact> closerContacts,
            List<IContact> fartherContacts,
            out String val,
            out IContact foundBy )
        {
            // As in, peer's nodes:
            // Exclude ourselves and the peers we're contacting (closerContacts and fartherContacts) to a get unique list of new peers.
            (List<IContact> contacts, IContact cFoundBy, String foundVal) = rpcCall( key, nodeToQuery );
            val = foundVal;
            foundBy = cFoundBy;
            var peersNodes = contacts.
                ExceptBy( Node.OurContact, c => c.KadId ).
                ExceptBy( nodeToQuery, c => c.KadId ).
                Except( closerContacts ).
                Except( fartherContacts ).ToList();

            // Null continuation is a special case primarily for unit testing when we have no nodes in any buckets.
            KademliaId nearestNodeDistance = nodeToQuery.KadId ^ key;

            lock ( m_locker )
            {
                closerContacts.
                    AddRangeDistinctBy( peersNodes.
                        Where( p => ( p.KadId ^ key ) < nearestNodeDistance ),
                        ( a, b ) => a.KadId == b.KadId );
            }

            lock ( m_locker )
            {
                fartherContacts.
                    AddRangeDistinctBy( peersNodes.
                        Where( p => ( p.KadId ^ key ) >= nearestNodeDistance ),
                        ( a, b ) => a.KadId == b.KadId );
            }

            return val != null;
        }

        public (List<IContact> contacts, IContact foundBy, String val) RpcFindNodes( KademliaId key, IContact contact )
        {
            (List<IContact> newContacts, RpcError timeoutError) = m_remoteProcedureCaller.FindNode( Node.OurContact, key, contact );

            // Null continuation here to support unit tests where a DHT hasn't been set up.
            Dht?.HandleError( timeoutError, contact );

            return (newContacts, null, null);
        }

        /// <summary>
        /// For each contact, call the FindNode and return all the nodes whose contacts responded
        /// within a "reasonable" period of time, unless a value is returned, at which point we stop.
        /// </summary>
        public (List<IContact> contacts, IContact foundBy, String val) RpcFindValue( KademliaId key, IContact contact )
        {
            var nodes = new List<IContact>();
            String retval = null;
            IContact foundBy = null;

            (List<IContact> otherContacts, String val, RpcError error) = m_remoteProcedureCaller.FindValue( Node.OurContact, key, contact );
            Dht.HandleError( error, contact );

            if ( !error.HasError )
            {
                if ( otherContacts != null )
                {
                    nodes.AddRange( otherContacts );
                }
                else
                {
                    Validate.IsTrue<ValueCannotBeNullException>( val != null, "Null values are not supported nor expected." );
                    nodes.Add( contact );           // The node we just contacted found the value.
                    foundBy = contact;
                    retval = val;
                }
            }

            return (nodes, foundBy, retval);
        }

        protected (Boolean found, List<IContact> closerContacts, IContact foundBy, String val) Query( KademliaId key, List<IContact> nodesToQuery, Func<KademliaId, IContact, (List<IContact> contacts, IContact foundBy, String val)> rpcCall, List<IContact> closerContacts, List<IContact> fartherContacts )
        {
            Boolean found = false;
            IContact foundBy = null;
            String val = String.Empty;

            foreach ( IContact n in nodesToQuery )
            {
                if ( GetCloserNodes( key, n, rpcCall, closerContacts, fartherContacts, out val, out foundBy ) )
                {
                    found = true;
                    break;
                }
            }

            return (found, closerContacts, foundBy, val);
        }
    }
}
