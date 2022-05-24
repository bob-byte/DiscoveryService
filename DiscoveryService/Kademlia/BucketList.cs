using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using DiscoveryServices.Kademlia.Exceptions;
using DiscoveryServices.Kademlia.Interfaces;
using LUC.Interfaces;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Discoveries;

using Newtonsoft.Json;

namespace DiscoveryServices.Kademlia
{
    class BucketList : AbstractKademlia, IBucketList
    {
        /// <summary>
        /// <seealso cref="KBucket"/>s of our <seealso cref="Node"/>. First <see cref="Buckets"/> has 1 full bucket then 
        /// it can be splited into more buckets during <see cref="AddContact(ref IContact)"/> method
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
        public IContact OurContact { get; set; }

        /// <summary>
        /// Allow to delay eviction and add to pending list of distributed hash table (DHT)
        /// </summary>
        [JsonIgnore]
        public IDht Dht { get; set; }

#if DEBUG       // For unit testing
        public BucketList( KademliaId id, IContact dummyContact, UInt16 protocolVersion )
            : base( protocolVersion )
        {
            OurID = id;
            OurContact = dummyContact;
            Buckets = new List<KBucket>
            {

                // First kbucket has max range.
                new KBucket()
            };
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
        /// IContact of current peer
        /// </param>
        public BucketList( IContact ourContact, UInt16 protocolVersion )
            : base( protocolVersion )
        {
            OurContact = ourContact;
            OurID = ourContact.KadId;
            Buckets = new List<KBucket>
            {

                // First kbucket has max range.
                new KBucket()
            };
        }

        /// <summary>
        /// Add a contact if possible, based on the algorithm described
        /// in sections 2.2, 2.4 and 4.2
        /// </summary>
        public void AddContact( IContact contact )
        {
#if !RECEIVE_UDP_FROM_OURSELF
            Validate.IsFalse<OurNodeCannotBeAContactException>( OurContact.Equals( contact ), "Cannot add ourselves as a contact!" );
#endif

            // Update the LastSeen to now.
            contact.Touch();

            BucketList bucketList = this;
            lock ( bucketList )
            {
                KBucket kbucket = GetKBucket( contact );

                if ( kbucket.Contains( contact ) )
                {
                    // Replace the existing contact, updating the network info and LastSeen timestamp.
                    kbucket.ReplaceContact( contact );
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
                        AddContact( contact );
                    }
                    else
                    {
                        IContact lastSeenContact = kbucket.Contacts.OrderBy( c => c.LastSeen ).First();
                        RpcError error = m_remoteProcedureCaller.Ping( OurContact, lastSeenContact );

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

        public KBucket GetKBucket( IContact contact )
        {
            lock ( this )
            {
                KBucket kBucket = Buckets.AsParallel().SingleOrDefault( c => c.Contains( contact ) );
                if ( kBucket == null )
                {
                    kBucket = Buckets[ GetKBucketIndex( contact.KadId ) ];
                }

                return kBucket;
            }
        }

        public KBucket GetKBucket( KademliaId otherID )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                return Buckets[ GetKBucketIndex( otherID ) ];
            }
        }

        public KBucket GetKBucket( BigInteger otherID )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                return Buckets[ GetKBucketIndex( otherID ) ];
            }
        }

        /// <summary>
        /// Returns true if the contact, by ID, exists in our bucket list.
        /// </summary>
        public Boolean ContactExists( IContact contact )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                return Buckets.SelectMany( b => b.Contacts ).Any( c => c.Equals( contact ) );
            }
        }

        protected virtual Boolean CanSplit( KBucket kbucket )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                return kbucket.HasInRange( OurID ) || ( ( kbucket.Depth() % DsConstants.B ) != 0 );
            }
        }

        private Int32 GetKBucketIndex( KademliaId otherID )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                return Buckets.FindIndex( b => b.HasInRange( otherID ) );
            }
        }

        private Int32 GetKBucketIndex( BigInteger otherID )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                return Buckets.FindIndex( b => b.HasInRange( otherID ) );
            }
        }

        /// <summary>
        /// Brute force distance lookup of all known contacts, sorted by distance, then we take at most k (20) of the closest.
        /// </summary>
        /// <param name="toFind">The ID for which we want to find close contacts.</param>
        /// <param name="machineIdForExclude">The ID to exclude (the requestor's ID)</param>
        public List<IContact> GetCloseContacts( KademliaId key, String machineIdForExclude )
        {
            BucketList bucketList = this;
            lock ( bucketList )
            {
                var contacts = Buckets.
                    SelectMany( b => b.Contacts ).
                    Where( c => !c.MachineId.Equals( machineIdForExclude, StringComparison.Ordinal ) ).
                    Select( c => new { contact = c, distance = c.KadId ^ key } ).
                    OrderBy( d => d.distance ).
                    Take( DsConstants.K );

                return contacts.Select( c => c.contact ).ToList();
            }
        }
    }
}
