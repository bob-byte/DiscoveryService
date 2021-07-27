using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using System;
using LUC.Interfaces;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.Services.Implementation;

namespace LUC.DiscoveryService.Kademlia
{
    public class Node : INode
    {
        private static LoggingService log;
        private readonly UInt32 protocolVersion;
        private readonly KademliaOperation kademliaOperation;

        public Contact OurContact { get { return ourContact; } set { ourContact = value; } }
        public IBucketList BucketList { get { return bucketList; } set { bucketList = value; } }
        public IStorage Storage { get { return storage; } set { storage = value; } }
        public IStorage CacheStorage { get { return cacheStorage; } set { cacheStorage = value; } }

        [JsonIgnore]
        public Dht Dht { get { return dht; } set { dht = value; } }

        protected Contact ourContact;
        protected IBucketList bucketList;
        protected IStorage storage;
        protected IStorage cacheStorage;
        protected Dht dht;

        /// <summary>
        /// For serialization.
        /// </summary>
        public Node()
        {
        }

        /// <summary>
        /// If cache storage is not explicity provided, we use an in-memory virtual storage.
        /// </summary>
        public Node(Contact contact, UInt16 protocolVersion,
            IStorage storage, IStorage cacheStorage = null)
        {
            ourContact = contact;
            bucketList = new BucketList(contact);
            this.storage = storage;
            this.cacheStorage = cacheStorage;

            if (cacheStorage == null)
            {
                this.cacheStorage = new VirtualStorage();
            }

            this.protocolVersion = protocolVersion;

            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };

            kademliaOperation = new KademliaOperation(protocolVersion);
        }

        /// <summary>
        /// Someone is pinging us.  Register the contact and respond.
        /// </summary>
        /// <returns>
        /// Current peer
        /// </returns>
        public Contact Ping(Contact sender)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(ref sender);

            return ourContact;
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
        /// 160-bit <seealso cref="ID"/> which we want to store
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
        public void Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            bucketList.AddContact(ref sender);

            if (isCached)
            {
                cacheStorage.Set(key, val, expirationTimeSec);
            }
            else
            {
                SendKeyValuesIfNewContact(sender);
                storage.Set(key, val, Constants.EXPIRATION_TIME_SECONDS);
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
        public (List<Contact> contacts, string val) FindNode(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(ref sender);

            // Exclude sender.
            var contacts = bucketList.GetCloseContacts(key, sender.ID);

            return (contacts, null);
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
        /// Returns either a list of close contacts or a the value, if the node's storage contains the value for the key.
        /// </returns>
        public (List<Contact> contacts, string val) FindValue(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(ref sender);

            if (storage.Contains(key))
            {
                return (null, storage.Get(key));
            }
            else if (CacheStorage.Contains(key))
            {
                return (null, CacheStorage.Get(key));
            }
            else
            {
                // Exclude sender.
                return (bucketList.GetCloseContacts(key, sender.ID), null);
            }
        }

#if DEBUG           // For unit testing
        public void SimpleStore(ID key, string val)
        {
            storage.Set(key, val);
        }
#endif

        /// <summary>
        /// For a new contact, we store values to that contact whose keys ^ ourContact are less than stored keys ^ [otherContacts].
        /// </summary>
        protected void SendKeyValuesIfNewContact(Contact sender)
        {
            List<Contact> contacts = new List<Contact>();

            if (IsNewContact(sender))
            {
                lock (bucketList)
                {
                    // Clone so we can release the lock.
                    contacts = new List<Contact>(bucketList.Buckets.SelectMany(b => b.Contacts));
                }

                if (contacts.Count() > 0)
                {
                    // and our distance to the key < any other contact's distance to the key...
                    storage.Keys.AsParallel().ForEach(k =>
                    {
                    // our min distance to the contact.
                    var distance = contacts.Min(c => k ^ c.ID);

                    // If our contact is closer, store the contact on its node.
                    if ((k ^ ourContact.ID) < distance)
                        {
                            var error = Store(ourContact, new ID(k), storage.Get(k), sender);
                            dht?.HandleError(error, sender);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Returns true if the contact isn't in the bucket list or the pending contacts list.
        /// </summary>
        protected bool IsNewContact(Contact sender)
        {
            bool ret;

            lock (bucketList)
            {
                // If we have a new contact...
                ret = bucketList.ContactExists(sender);
            }

            if (dht != null)            // for unit testing, dht may be null
            {
                lock (dht.PendingContacts)
                {
                    ret |= dht.PendingContacts.ContainsBy(sender, c => c.ID);
                }
            }

            return !ret;
        }

        /// <param name="ourContact">
        /// Current peer
        /// </param>
        public RpcError PingRemoteContact(Contact ourContact, Contact remoteContact) =>
            kademliaOperation.Ping(ourContact, remoteContact);

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, Contact remoteContact, bool isCached = false, int expirationTimeSec = 0) =>
            kademliaOperation.Store(sender, key, val, remoteContact, isCached, expirationTimeSec);

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, Contact remoteContact) => kademliaOperation.FindNode(sender, keyToFindContacts, remoteContact);

        /// <inheritdoc/>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID keyToFindContact, Contact remoteContact) =>
            kademliaOperation.FindValue(sender, keyToFindContact, remoteContact);
    }
}