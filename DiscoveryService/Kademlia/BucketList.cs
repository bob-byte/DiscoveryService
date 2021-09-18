using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Kademlia.Interfaces;

using Newtonsoft.Json;

namespace LUC.DiscoveryService.Kademlia
{
    class BucketList : AbstractKademlia, IBucketList
    {
        /// <summary>
        /// <seealso cref="Kademlia.KBucket"/>s of our <seealso cref="Node"/>. First <see cref="Buckets"/> has 1 full bucket then 
        /// it can be splited into more buckets during <see cref="AddContact(ref Contact)"/> method
        /// </summary>
        public List<KBucket> Buckets { get; set; }

        /// <summary>
        /// <see cref="ID"/> of <seealso cref="OurContact"/>
        /// </summary>
        [JsonIgnore]
        public KademliaId OurID { get; set; }

        /// <summary>
        /// IP-addresses, TCP port, and ID which we use to listen and send messages 
        /// </summary>
        [JsonIgnore]
        public Contact OurContact { get; set; }

        /// <summary>
        /// Allow to delay eviction and add to pending list of distributed hash table (DHT)
        /// </summary>
        [JsonIgnore]
        public IDht Dht { get; set; }

#if DEBUG       // For unit testing
        public BucketList( KademliaId id, Contact dummyContact, UInt16 protocolVersion )
            : base( protocolVersion )
        {
            OurID = id;
            OurContact = dummyContact;
            Buckets = new List<KBucket>();

            // First kbucket has max range.
            Buckets.Add( new KBucket() );
        }
#endif

        /// <summary>
        /// For serialization.
        /// </summary>
        public BucketList()
            : base( protocolVersion: 1 )
        {
            ;// do nothing
        }

        /// <summary>
        /// Initialize the bucket list with our host ID and create a single bucket for the full ID range.
        /// </summary>
        /// /// <param name="ourContact">
        /// Contact of current peer
        /// </param>
        public BucketList( Contact ourContact, UInt16 protocolVersion )
            : base( protocolVersion )
        {
            OurContact = ourContact;
            OurID = ourContact.KadId;
            Buckets = new List<KBucket>();

            // First kbucket has max range.
            Buckets.Add( new KBucket() );
        }

        /// <summary>
        /// Add a contact if possible, based on the algorithm described
        /// in sections 2.2, 2.4 and 4.2
        /// </summary>
        public void AddContact( ref Contact contact )
        {
#if !RECEIVE_TCP_FROM_OURSELF
            Validate.IsFalse<OurNodeCannotBeAContactException>(ourContact.MachineId == contact.MachineId, "Cannot add ourselves as a contact!");
#endif

            // Update the LastSeen to now.
            contact.Touch();

            lock ( this )
            {
                KBucket kbucket = GetKBucket( contact.KadId );

                if ( kbucket.Contains( contact.MachineId ) )
                {
                    // Replace the existing contact, updating the network info and LastSeen timestamp.
                    kbucket.ReplaceContact( ref contact );
                }
                else if ( kbucket.IsBucketFull )
                {
                    if ( CanSplit( kbucket ) )
                    {
                        // Split the bucket and try again.
                        (KBucket k1, KBucket k2) = kbucket.Split();
                        Int32 idx = GetKBucketIndex( contact.KadId );
                        Buckets[ idx ] = k1;
                        Buckets.Insert( idx + 1, k2 );
                        Buckets[ idx ].Touch();
                        Buckets[ idx + 1 ].Touch();
                        AddContact( ref contact );
                    }
                    else
                    {
                        Contact lastSeenContact = kbucket.Contacts.OrderBy( c => c.LastSeen ).First();
                        RpcError error = m_clientKadOperation.Ping( OurContact, lastSeenContact );

                        if ( error.HasError )
                        {
                            // Null continuation is used because unit tests may not initialize a DHT.
                            Dht?.DelayEviction( lastSeenContact, contact );
                        }
                        else
                        {
                            // Still can't add the contact, so put it into the pending list.
                            Dht?.AddToPending( contact );
                        }
                    }
                }
                else
                {
                    // Bucket isn't full, so just add the contact.
                    kbucket.AddContact( contact );
                }
            }
        }

        public KBucket GetKBucket( KademliaId otherID )
        {
            lock ( this )
            {
                return Buckets[ GetKBucketIndex( otherID ) ];
            }
        }

        public KBucket GetKBucket( BigInteger otherID )
        {
            lock ( this )
            {
                return Buckets[ GetKBucketIndex( otherID ) ];
            }
        }

        /// <summary>
        /// Returns true if the contact, by ID, exists in our bucket list.
        /// </summary>
        public Boolean ContactExists( Contact sender )
        {
            lock ( this )
            {
                return Buckets.SelectMany( b => b.Contacts ).Any( c => c.KadId == sender.KadId );
            }
        }

        protected virtual Boolean CanSplit( KBucket kbucket )
        {
            lock ( this )
            {
                return kbucket.HasInRange( OurID ) || ( ( kbucket.Depth() % Constants.B ) != 0 );
            }
        }

        protected Int32 GetKBucketIndex( KademliaId otherID )
        {
            lock ( this )
            {
                return Buckets.FindIndex( b => b.HasInRange( otherID ) );
            }
        }

        protected Int32 GetKBucketIndex( BigInteger otherID )
        {
            lock ( this )
            {
                return Buckets.FindIndex( b => b.HasInRange( otherID ) );
            }
        }

        /// <summary>
        /// Brute force distance lookup of all known contacts, sorted by distance, then we take at most k (20) of the closest.
        /// </summary>
        /// <param name="toFind">The ID for which we want to find close contacts.</param>
        /// <param name="exclude">The ID to exclude (the requestor's ID)</param>
        public List<Contact> GetCloseContacts( KademliaId key, KademliaId exclude )
        {
            lock ( this )
            {
                var contacts = Buckets.
                    SelectMany( b => b.Contacts ).
                    Where( c => c.KadId != exclude ).
                    Select( c => new { contact = c, distance = c.KadId ^ key } ).
                    OrderBy( d => d.distance ).
                    Take( Constants.K );

                return contacts.Select( c => c.contact ).ToList();
            }
        }
    }
}
