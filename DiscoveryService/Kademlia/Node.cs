﻿using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using System;
using LUC.Interfaces;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.Services.Implementation;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Kademlia.Interfaces;

namespace LUC.DiscoveryService.Kademlia
{
    class Node : AbstractKademlia, INode
    {
        public Contact OurContact { get; set; }

        public IBucketList BucketList { get; set; }

        public IStorage Storage { get; set; }

        public IStorage CacheStorage { get; set; }

        [JsonIgnore]
        public Dht Dht { get; set; }

        /// <summary>
        /// For serialization.
        /// </summary>
        public Node()
            : base(protocolVersion: 1)
        {
            ;//do nothing
        }

        /// <summary>
        /// If cache storage is not explicity provided, we use an in-memory virtual storage.
        /// </summary>
        public Node( Contact contact, UInt16 protocolVersion, IStorage storage, IStorage cacheStorage = null )
            : base( protocolVersion )
        {
            OurContact = contact;
            BucketList = new BucketList( contact, protocolVersion );
            Storage = storage;
            CacheStorage = cacheStorage;

            if ( cacheStorage == null )
            {
                CacheStorage = new VirtualStorage();
            }
        }

        /// <summary>
        /// Someone is pinging us.  Register the contact and respond.
        /// </summary>
        public void Ping( Contact sender )
        {
#if !RECEIVE_TCP_FROM_OURSELF
            Validate.IsFalse<SendingQueryToSelfException>(sender.MachineId == ourContact.MachineId, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
#endif

            BucketList.AddContact( ref sender );
        }

        /// <summary>
        /// Insturcts a node to store a (<paramref name="key"/>, <paramref name="val"/>) pair for later retrieval. 
        /// “To store a (<paramref name="key"/>, <paramref name="val"/>) pair, a participant locates the k closest nodes to the key and sends them STORE RPCS.” 
        /// The participant does this by inspecting its own k-closest nodes to the key.
        /// Store a key-value pair in the republish or cache storage.
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit <seealso cref="KademliaId"/> which we want to store
        /// </param>
        /// <param name="val">
        /// Value of the peer which we want to store
        /// </param>
        /// <param name="isCached">
        /// Whether cach a (<paramref name="key"/>, <paramref name="val"/>) pair
        /// </param>
        /// <param name="expirationTimeSec">
        /// Time of expiration cach in seconds
        /// </param>
        /// <returns>
        /// Information about the errors which maybe happened
        /// </returns>
        public void Store( Contact sender, KademliaId key, String val, Boolean isCached = false, Int32 expirationTimeSec = 0 )
        {
#if !RECEIVE_TCP_FROM_OURSELF
            Validate.IsFalse<SendingQueryToSelfException>(sender.MachineId == ourContact.MachineId, "Sender should not be ourself!");
            if(!isCached)
            {
                SendKeyValuesIfNewContact(sender);
            }
#endif

            BucketList.AddContact( ref sender );

            if ( isCached )
            {
                CacheStorage.Set( key, val, expirationTimeSec );
            }
            else
            {
                Storage.Set( key, val, Constants.EXPIRATION_TIME_SECONDS );
            }
        }

        /// <summary>
        /// This operation has two purposes:
        /// <list type="bullet">
        /// <item>
        /// A peer can issue this RPC(remote procedure call) on contacts it knows about, updating its own list of "close" peers
        /// </item>
        /// <item>
        /// A peer may issue this RPC to discover other peers on the network
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit ID near which you want to get list of contacts
        /// </param>
        /// <returns>
        /// <see cref="Constants.K"/> nodes the recipient of the RPC  knows about closest to the target <paramref name="key"/>. 
        /// Contacts can come from a single k-bucket, or they may come from multiple k-buckets if the closest k-bucket is not full. 
        /// In any case, the RPC recipient must return k items 
        /// (unless there are fewer than k nodes in all its k-buckets combined, in which case it returns every node it knows about).
        /// Also this operation returns <seealso cref="RpcError"/> to inform about the errors which maybe happened
        /// </returns>
        //TODO it shouldn't return val
        public void FindNode( Contact sender, KademliaId key, out List<Contact> contacts )
        {
#if !RECEIVE_TCP_FROM_OURSELF
            Validate.IsFalse<SendingQueryToSelfException>(sender.MachineId == ourContact.MachineId, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
#endif

            BucketList.AddContact( ref sender );

            contacts = BucketList.GetCloseContacts( key, exclude: sender.ID );
        }

        /// <summary>
        /// Attempt to find the value in the peer network. Also this operation has two purposes:
        /// <list type="bullet">
        /// <item>
        /// A peer can issue this RPC(remote procedure call) on contacts it knows about, updating its own list of "close" peers
        /// </item>
        /// <item>
        /// A peer may issue this RPC to discover other peers on the network
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit ID near which you want to get list of contacts
        /// </param>
        /// <returns>
        /// Returns either the value if <paramref name="key"/> was storaged using <seealso cref="Store(Contact, KademliaId, String, Boolean, Int32)"/> (first purpose) or a list of close contacts if it wasn't (second purpose)
        /// </returns>
        public void FindValue( Contact sender, KademliaId key, out List<Contact> contacts, out String nodeValue )
        {
#if !RECEIVE_TCP_FROM_OURSELF
            Validate.IsFalse<SendingQueryToSelfException>(sender.MachineId == ourContact.MachineId, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
#endif

            BucketList.AddContact( ref sender );

            if ( Storage.Contains( key ) )
            {
                contacts = null;
                nodeValue = Storage.Get( key );
            }
            else if ( CacheStorage.Contains( key ) )
            {
                contacts = null;
                nodeValue = CacheStorage.Get( key );
            }
            else
            {
                contacts = BucketList.GetCloseContacts( key, exclude: sender.ID );
                nodeValue = null;
            }
        }

#if DEBUG           // For unit testing
        public void SimpleStore( KademliaId key, String val ) => 
            Storage.Set( key, val );
#endif

        /// <summary>
        /// For a new contact, we store values to that contact whose keys ^ ourContact are less than stored keys ^ [otherContacts].
        /// </summary>
        protected void SendKeyValuesIfNewContact( Contact sender )
        {
            List<Contact> contacts = new List<Contact>();

            if ( IsNewContact( sender ) )
            {
                lock ( BucketList )
                {
                    // Clone so we can release the lock.
                    contacts = new List<Contact>( BucketList.Buckets.SelectMany( b => b.Contacts ) );
                }

                if ( contacts.Count() > 0 )
                {
                    // and our distance to the key < any other contact's distance to the key...
                    Storage.Keys.AsParallel().ForEach( (Action<System.Numerics.BigInteger>)(k =>
                     {
                         // our min distance to the contact.
                         KademliaId distance = contacts.Min( c => k ^ c.ID );

                         // If our contact is closer, store the contact on its node.
                         if ( ( k ^ this.OurContact.ID ) < distance )
                         {
                             RpcError error = m_clientKadOperation.Store( (Contact)this.OurContact, new KademliaId( k ), Storage.Get( k ), sender );
                             Dht?.HandleError( error, sender );
                         }
                     }) );
                }
            }
        }

        /// <summary>
        /// Returns true if the contact isn't in the bucket list or the pending contacts list.
        /// </summary>
        protected Boolean IsNewContact( Contact sender )
        {
            Boolean ret;

            lock ( BucketList )
            {
                // If we have a new contact...
                ret = BucketList.ContactExists( sender );
            }

            if ( Dht != null )            // for unit testing, dht may be null
            {
                lock ( Dht.PendingContacts )
                {
                    ret |= Dht.PendingContacts.ContainsBy( sender, c => c.ID );
                }
            }

            return !ret;
        }
    }
}