using System.Collections.Generic;

namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    /// <summary>
    /// A high-level singleton container for buckets and operations that manipulate buckets
    /// </summary>
    public interface IBucketList
    {
        List<KBucket> Buckets { get; }
        IDht Dht { get; set; }
        ID OurID { get; set; }
        Contact OurContact { get; set; }
        void AddContact(ref Contact contact);
        KBucket GetKBucket(ID otherID);
        List<Contact> GetCloseContacts(ID key, ID exclude);
        bool ContactExists(Contact contact);
    }
}
