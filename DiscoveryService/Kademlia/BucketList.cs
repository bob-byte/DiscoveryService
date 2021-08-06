using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LUC.DiscoveryService.Kademlia.Interfaces;
using Newtonsoft.Json;

namespace LUC.DiscoveryService.Kademlia
{
    public class BucketList : AbstractKademlia, IBucketList
    {
        /// <summary>
        /// <seealso cref="Kademlia.KBucket"/>s of our <seealso cref="Node"/>. First <see cref="Buckets"/> has 1 full bucket then 
        /// it can be splited into more buckets during <see cref="AddContact(ref Contact)"/> method
        /// </summary>
        public List<KBucket> Buckets { get { return buckets; } set { buckets = value; } }

        /// <summary>
        /// <see cref="ID"/> of <seealso cref="OurContact"/>
        /// </summary>
        [JsonIgnore]
        public ID OurID { get { return ourID; } set { ourID = value; } }

        /// <summary>
        /// IP-addresses, TCP port, and ID which we use to listen and send messages 
        /// </summary>
        [JsonIgnore]
        public Contact OurContact { get { return ourContact; } set { ourContact = value; } }

        /// <summary>
        /// Allow to delay eviction and add to pending list of distributed hash table (DHT)
        /// </summary>
        [JsonIgnore]
        public IDht Dht { get { return dht; } set { dht = value; } }

        protected List<KBucket> buckets;
        protected ID ourID;
        protected Contact ourContact;
        protected IDht dht;

#if DEBUG       // For unit testing
        public BucketList(ID id, Contact dummyContact)
        {
            ourID = id;
            ourContact = dummyContact;
            buckets = new List<KBucket>();

            // First kbucket has max range.
            buckets.Add(new KBucket());
        }
#endif

        /// <summary>
        /// For serialization.
        /// </summary>
        public BucketList()
        {
        }

        /// <summary>
        /// Initialize the bucket list with our host ID and create a single bucket for the full ID range.
        /// </summary>
        /// <param name="ourContact">
        /// Contact of current peer
        /// </param>
        public BucketList(Contact ourContact)
        {
            this.ourContact = ourContact;
            ourID = ourContact.ID;
            buckets = new List<KBucket>();

            // First kbucket has max range.
            buckets.Add(new KBucket());
        }

        /// <summary>
        /// Add a contact if possible, based on the algorithm described
        /// in sections 2.2, 2.4 and 4.2
        /// </summary>
        /// <param name="contact">
        /// <see cref="Contact"/> which we try to add to any bucket
        /// </param>
        public void AddContact(ref Contact contact)
        {
            Validate.IsFalse<OurNodeCannotBeAContactException>(ourID == contact.ID, "Cannot add ourselves as a contact!");
            contact.Touch();            // Update the LastSeen to now.

            lock (this)
            {
                KBucket kbucket = GetKBucket(contact.ID);

                if (kbucket.Contains(contact.ID))
                {
                    // Replace the existing contact, updating the network info and LastSeen timestamp.
                    kbucket.ReplaceContact(ref contact);
                }
                else if (kbucket.IsBucketFull)
                {
                    if (CanSplit(kbucket))
                    {
                        // Split the bucket and try again.
                        (KBucket k1, KBucket k2) = kbucket.Split();
                        int idx = GetKBucketIndex(contact.ID);
                        buckets[idx] = k1;
                        buckets.Insert(idx + 1, k2);
                        buckets[idx].Touch();
                        buckets[idx + 1].Touch();
                        AddContact(ref contact);
                    }
                    else
                    {
                        Contact lastSeenContact = kbucket.Contacts.OrderBy(c => c.LastSeen).First();
                        RpcError error = clientKadOperation.Ping(ourContact, lastSeenContact);

                        if (error.HasError)
                        {
                            // Null continuation is used because unit tests may not initialize a DHT.
                            dht?.DelayEviction(lastSeenContact, contact);
                        }
                        else
                        {
                            // Still can't add the contact, so put it into the pending list.
                            dht?.AddToPending(contact);
                        }
                    }
                }
                else
                {
                    // Bucket isn't full, so just add the contact.
                    kbucket.AddContact(contact);
                }
            }
        }

        /// <summary>
        /// Get <see cref="Kademlia.KBucket"/> in which <paramref name="otherID"/> is contained
        /// </summary>
        /// <param name="otherID">
        /// <see cref="ID"/> by which you want to find <see cref="Kademlia.KBucket"/>
        /// </param>
        /// <returns>
        /// <see cref="Kademlia.KBucket"/> in which <paramref name="otherID"/> is contained
        /// </returns>
        public KBucket GetKBucket(ID otherID)
        {
            lock (this)
            {
                return buckets[GetKBucketIndex(otherID)];
            }
		}

        /// <summary>
        /// Get <see cref="Kademlia.KBucket"/> in which <paramref name="otherID"/> is contained
        /// </summary>
        /// <param name="otherID">
        /// <see cref="ID"/> by which you want to find <see cref="Kademlia.KBucket"/>
        /// </param>
        /// <returns>
        /// <see cref="Kademlia.KBucket"/> in which <paramref name="otherID"/> is contained
        /// </returns>
        public KBucket GetKBucket(BigInteger otherID)
        {
            lock (this)
            {
                return buckets[GetKBucketIndex(otherID)];
            }
        }

        /// <summary>
        /// Returns true if the contact, by ID, exists in our bucket list.
        /// </summary>
        /// <param name="sender">
        /// Defines whether contact exist in any <see cref="Buckets"/>
        /// </param>
        public bool ContactExists(Contact sender)
        {
            lock (this)
            {
                return Buckets.SelectMany(b => b.Contacts).Any(c => c.ID == sender.ID);
            }
        }

        protected virtual bool CanSplit(KBucket kbucket)
        {
            lock (this)
            {
                return kbucket.HasInRange(ourID) || ((kbucket.Depth() % Constants.B) != 0);
            }
        }

        protected int GetKBucketIndex(ID otherID)
		{
            lock (this)
            {
                return buckets.FindIndex(b => b.HasInRange(otherID));
            }
		}

        protected int GetKBucketIndex(BigInteger otherID)
        {
            lock (this)
            {
                return buckets.FindIndex(b => b.HasInRange(otherID));
            }
        }

        /// <summary>
        /// Brute force distance lookup of all known contacts, sorted by distance, then we take at most k (20) of the closest.
        /// </summary>
        /// <param name="toFind">The ID for which we want to find close contacts.</param>
        /// <param name="exclude">The ID to exclude (the requestor's ID)</param>
        public List<Contact> GetCloseContacts(ID key, ID exclude)
        {
            lock (this)
            {
                var contacts = buckets.
                    SelectMany(b => b.Contacts).
                    Where(c => c.ID != exclude).
                    Select(c => new { contact = c, distance = c.ID ^ key }).
                    OrderBy(d => d.distance).
                    Take(Constants.K);

                return contacts.Select(c => c.contact).ToList();
            }
        }
    }
}
