using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Clifton.Kademlia.Common;
using LUC.DiscoveryService.Kademlia.Protocols;
using System;

namespace LUC.DiscoveryService.Kademlia
{
    public class Node : INode
    {
        public List<Contact> OurContacts { get; set; }
        public List<IBucketList> BucketLists { get; set; }
        public IStorage Storage { get; set; }
        public IStorage CacheStorage { get { return cacheStorage; } set { cacheStorage = value; } }

        [JsonIgnore]
        public Dht Dht { get { return dht; } set { dht = value; } }

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
        public Node(IEnumerable<Contact> contacts, IStorage storage, IStorage cacheStorage = null)
        {
            OurContacts = contacts.ToList();
            BucketLists = new List<IBucketList>();
            for (int i = 0; i < OurContacts.Count; i++)
            {
                BucketLists[i] = new BucketList(OurContacts[i]);
            }
            this.Storage = storage;
            this.cacheStorage = cacheStorage;

            if (cacheStorage == null)
            {
                this.cacheStorage = new VirtualStorage();
            }
        }

        // ======= Server Entry Points =======

        public object ServerPing(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            Ping(new Contact(protocol, new ID(request.Sender)));

            return new { RandomID = request.RandomID };
        }

        public object ServerStore(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            Store(new Contact(protocol, new ID(request.Sender)), new ID(request.Key), request.Value, request.IsCached, request.ExpirationTimeSec);

            return new { RandomID = request.RandomID };
        }

        public object ServerFindNode(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            var (contacts, val) = FindNode(new Contact(protocol, new ID(request.Sender)), new ID(request.Key));

            return new
            {
                Contacts = contacts.Select(c =>
                    new
                    {
                        Contact = c.ID.Value,
                        Protocol = c.Protocol,
                        ProtocolName = c.Protocol.GetType().Name
                    }).ToList(),
                RandomID = request.RandomID
            };
        }

        public object ServerFindValue(CommonRequest request)
        {
            IProtocol protocol = Protocol.InstantiateProtocol(request.Protocol, request.ProtocolName);
            var (contacts, val) = FindValue(new Contact(protocol, new ID(request.Sender)), new ID(request.Key));

            return new
            {
                Contacts = contacts?.Select(c =>
                new
                {
                    Contact = c.ID.Value,
                    Protocol = c.Protocol,
                    ProtocolName = c.Protocol.GetType().Name
                })?.ToList(),
                RandomID = request.RandomID,
                Value = val
            };
        }

        // ======= ======= ======= ======= =======

        /// <summary>
        /// Someone is pinging us.  Register the contact and respond.
        /// </summary>
        public List<Contact> Ping(Contact sender)
        {
            Validate.IsFalse<SendingQueryToSelfException>(OurContacts.Any((ourCountact) => ourCountact.ID == sender.ID), "Sender should not be ourself!");
            
            SendKeyValuesIfNewContact(sender);

            foreach (var bucketList in BucketLists)
            {
                bucketList.AddContact(sender);
            }

            return OurContacts;
        }

        /// <summary>
        /// Store a key-value pair in the republish or cache storage.
        /// </summary>
        public void Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            Validate.IsFalse<SendingQueryToSelfException>(OurContacts.Any((ourCountact) => ourCountact.ID == sender.ID), "Sender should not be ourself!");
            foreach (var bucketList in BucketLists)
            {
                bucketList.AddContact(sender);
            }

            if (isCached)
            {
                cacheStorage.Set(key, val, expirationTimeSec);
            }
            else
            {
                SendKeyValuesIfNewContact(sender);
                Storage.Set(key, val, Constants.EXPIRATION_TIME_SECONDS);
            }
        }

        /// <summary>
        /// From the spec: FindNode takes a 160-bit ID as an argument. The recipient of the RPC returns (IP address, UDP port, Node ID) triples 
        /// for the k nodes it knows about closest to the target ID. These triples can come from a single k-bucket, or they may come from 
        /// multiple k-buckets if the closest k-bucket is not full. In any case, the RPC recipient must return k items (unless there are 
        /// fewer than k nodes in all its k-buckets combined, in which case it returns every node it knows about).
        /// </summary>
        /// <returns></returns>
        public (List<Contact> contacts, string val) FindNode(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(OurContacts.Any((ourCountact) => ourCountact.ID == sender.ID), "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            foreach (var bucketList in BucketLists)
            {
                bucketList.AddContact(sender);
            }

            // Exclude sender.
            List<Contact> contacts = CloseContacts(key, sender.ID);

            return (contacts, null);
        }

        /// <summary>
        /// Returns either a list of close contacts or a the value, if the node's storage contains the value for the key.
        /// </summary>
        public (List<Contact> contacts, string val) FindValue(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(OurContacts.Any((ourCountact) => ourCountact.ID == sender.ID), "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);

            foreach (var bucketList in BucketLists)
            {
                bucketList.AddContact(sender);
            }

            if (Storage.Contains(key))
            {
                return (null, Storage.Get(key));
            }
            else if (CacheStorage.Contains(key))
            {
                return (null, CacheStorage.Get(key));
            }
            else
            {
                // Exclude sender.
                return (CloseContacts(key, sender.ID), null);
            }
        }

        public List<Contact> CloseContacts(ID key, ID exclude)
        {
            lock(Lock.LockGetCloseContacts)
            {
                return BucketLists.SelectMany(c => c.GetCloseContacts(key, exclude)).
                    Select(c => new { Contact = c, Distance = c.ID ^ key }).
                    OrderBy(c => c.Distance).
                    Take(Constants.K).
                    Select(c => c.Contact).ToList();
            }
        }

#if DEBUG           // For unit testing
        public void SimpleStore(ID key, string val)
        {
            Storage.Set(key, val);
        }
#endif

        /// <summary>
        /// For a new contact, we store values to that contact whose keys ^ ourContact are less than stored keys ^ [otherContacts].
        /// </summary>
        protected void SendKeyValuesIfNewContact(IBucketList bucketList, Contact ourContact, Contact sender)
        {
            List<Contact> contacts = new List<Contact>();

            if (IsNewContact(bucketList, sender))
            {
                lock (BucketLists)
                {
                    // Clone so we can release the lock.
                    contacts = new List<Contact>(bucketList.Buckets.SelectMany(b => b.Contacts));
                }

                if (contacts.Count() > 0)
                {
                    // and our distance to the key < any other contact's distance to the key...
                    Storage.Keys.AsParallel().ForEach(k =>
                    {
                        // our min distance to the contact.
                        var distance = contacts.Min(c => k ^ c.ID);

                        // If our contact is closer, store the contact on its node.
                        if ((k ^ ourContact.ID) < distance)
                        {
                            var error = sender.Protocol.Store(ourContact, new ID(k), Storage.Get(k));
                            dht?.HandleError(error, sender);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Returns true if the contact isn't in the bucket list or the pending contacts list.
        /// </summary>
        protected bool IsNewContact(IBucketList bucketList, Contact sender)
        {
            bool ret;

            lock (BucketLists)
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
    }
}