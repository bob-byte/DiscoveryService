using System;
using System.Collections.Generic;
using System.Linq;

using LUC.DiscoveryService.Kademlia.Exceptions;

using Newtonsoft.Json;

namespace LUC.DiscoveryService.Kademlia.Routers
{
    abstract class BaseRouter : AbstractKademlia
    {
        protected Object m_locker = new Object();

#if DEBUG       // for unit testing
        [JsonIgnore]
        public List<Contact> CloserContacts { get; protected set; }

        [JsonIgnore]
        public List<Contact> FartherContacts { get; protected set; }
#endif

        public Node Node { get; set; }

        [JsonIgnore]
        public Dht Dht { get; set; }

        public BaseRouter( UInt16 protocolVersion )
            : base( protocolVersion )
        {
            ;//do nothing
        }

        public abstract (Boolean found, List<Contact> contacts, Contact foundBy, String val) Lookup(
            KademliaId key,
            Func<KademliaId, Contact, (List<Contact> contacts, Contact foundBy, String val)> rpcCall,
            Boolean giveMeAll = false );

        /// <summary>
        /// Using the k-bucket's key (it's high value), find the closest 
        /// k-bucket the given key that isn't empty.
        /// </summary>
#if DEBUG           // For unit testing.
        public virtual KBucket FindClosestNonEmptyKBucket( KademliaId key )
#else
        protected virtual KBucket FindClosestNonEmptyKBucket(ID key)
#endif
        {
            KBucket closest = Node.BucketList.Buckets.Where( b => b.Contacts.Count > 0 ).OrderBy( b => b.Key ^ key ).FirstOrDefault();
            Validate.IsTrue<NoNonEmptyBucketsException>( closest != null, "No non-empty buckets exist.  You must first register a peer and add that peer to your bucketlist." );

            return closest;
        }

        /// <summary>
        /// Get sorted list of closest nodes to the given key.
        /// </summary>
#if DEBUG           // For unit testing.
        public List<Contact> GetClosestNodes( KademliaId key, KBucket bucket )
#else
        protected List<Contact> GetClosestNodes(ID key, KBucket bucket)
#endif
        {
            return bucket.Contacts.OrderBy( c => c.ID ^ key ).ToList();
        }

        public Boolean GetCloserNodes(
            KademliaId key,
            Contact nodeToQuery,
            Func<KademliaId, Contact, (List<Contact> contacts, Contact foundBy, String val)> rpcCall,
            List<Contact> closerContacts,
            List<Contact> fartherContacts,
            out String val,
            out Contact foundBy )
        {
            // As in, peer's nodes:
            // Exclude ourselves and the peers we're contacting (closerContacts and fartherContacts) to a get unique list of new peers.
            (List<Contact> contacts, Contact cFoundBy, String foundVal) = rpcCall( key, nodeToQuery );
            val = foundVal;
            foundBy = cFoundBy;
            List<Contact> peersNodes = contacts.
                ExceptBy( Node.OurContact, c => c.ID ).
                ExceptBy( nodeToQuery, c => c.ID ).
                Except( closerContacts ).
                Except( fartherContacts ).ToList();

            // Null continuation is a special case primarily for unit testing when we have no nodes in any buckets.
            KademliaId nearestNodeDistance = nodeToQuery.ID ^ key;

            lock ( m_locker )
            {
                closerContacts.
                    AddRangeDistinctBy( peersNodes.
                        Where( p => ( p.ID ^ key ) < nearestNodeDistance ),
                        ( a, b ) => a.ID == b.ID );
            }

            lock ( m_locker )
            {
                fartherContacts.
                    AddRangeDistinctBy( peersNodes.
                        Where( p => ( p.ID ^ key ) >= nearestNodeDistance ),
                        ( a, b ) => a.ID == b.ID );
            }

            return val != null;
        }

        public (List<Contact> contacts, Contact foundBy, String val) RpcFindNodes( KademliaId key, Contact contact )
        {
            (List<Contact> newContacts, RpcError timeoutError) = m_clientKadOperation.FindNode( Node.OurContact, key, contact );

            // Null continuation here to support unit tests where a DHT hasn't been set up.
            Dht?.HandleError( timeoutError, contact );

            return (newContacts, null, null);
        }

        /// <summary>
        /// For each contact, call the FindNode and return all the nodes whose contacts responded
        /// within a "reasonable" period of time, unless a value is returned, at which point we stop.
        /// </summary>
        public (List<Contact> contacts, Contact foundBy, String val) RpcFindValue( KademliaId key, Contact contact )
        {
            List<Contact> nodes = new List<Contact>();
            String retval = null;
            Contact foundBy = null;

            (List<Contact> otherContacts, String val, RpcError error) = m_clientKadOperation.FindValue( Node.OurContact, key, contact );
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

        protected (Boolean found, List<Contact> closerContacts, Contact foundBy, String val) Query( KademliaId key, List<Contact> nodesToQuery, Func<KademliaId, Contact, (List<Contact> contacts, Contact foundBy, String val)> rpcCall, List<Contact> closerContacts, List<Contact> fartherContacts )
        {
            Boolean found = false;
            Contact foundBy = null;
            String val = String.Empty;

            foreach ( Contact n in nodesToQuery )
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
