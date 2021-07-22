using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;

namespace LUC.DiscoveryService.Kademlia
{
    //public interface IKBucket
    //{
    //    BigInteger Low { get; }
    //    BigInteger High { get; }
    //}

    public interface IDht
    {
        Node Node { get; set; }
        void DelayEviction(Contact toEvict, Contact toReplace);
        void AddToPending(Contact pending);
    }

    public interface IBucketList
    {
        List<KBucket> Buckets { get; }
        IDht Dht { get; set; }
        ID OurID { get; set; }
        Contact OurContact { get; set; }
        void AddContact(Contact contact);
        KBucket GetKBucket(ID otherID);
        List<Contact> GetCloseContacts(ID key, ID exclude);
        bool ContactExists(Contact contact);
    }

    public interface INode
    {
        Contact OurContact { get; }
        IBucketList BucketList { get; }
    }

    public interface IStorage
    {
        bool Contains(ID key);
        bool TryGetValue(ID key, out string val);
        string Get(ID key);
        string Get(BigInteger key);
        DateTime GetTimeStamp(BigInteger key);
        void Set(ID key, string value, int expirationTimeSec = 0);
        int GetExpirationTimeSec(BigInteger key);
        void Remove(BigInteger key);
        List<BigInteger> Keys { get; }

        /// <summary>
        /// Updates the republish timestamp.
        /// </summary>
        void Touch(BigInteger key);
    }
}
