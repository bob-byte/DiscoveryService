using LUC.DiscoveryService.Kademlia.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;

namespace LUC.DiscoveryService.Kademlia
{
    public class Contact  : IComparable
    {
        public DateTime LastSeen { get; set; }
        public ID ID { get; set; }

        public IProtocol Protocol { get; set; }
        public ICollection<EndPoint> LocalEndPoints { get; set; }

        // For serialization.  Don't want to use JsonConstructor because we don't want to touch the LastSeen.
        public Contact()
        {
        }

        /// <summary>
        /// Initialize a contact with its protocol and ID.
        /// </summary>
        public Contact(IProtocol protocol, ID contactID, ICollection<EndPoint> endPoints)
        {
            Protocol = protocol;
            ID = contactID;
            LocalEndPoints = endPoints;

            Touch();
        }

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            LastSeen = DateTime.Now;
        }

        // IComparable and operator overloading is implemented because on deserialization, Contact instances
        // are all unique but we need to be able to compare our DHT's contacts, so without worrying about
        // whether we're comparing Contact references or their ID's, we're doing it correctly here.

        public int CompareTo(object obj)
        {
            Validate.IsTrue<NotContactException>(obj is Contact, "Cannot compare non-Contact objects to a Contact");

            Contact c = (Contact)obj;

            return ID.CompareTo(c.ID);
        }

        public override String ToString()
        {
            return $"{nameof(ID)} = {ID};\n" +
                   $"{nameof(LocalEndPoints)} = {LocalEndPoints};\n" +
                   $"{nameof(LastSeen)} = {LastSeen};\n" +
                   $"{nameof(Protocol)} = {Protocol}";
        }

        public static bool operator ==(Contact a, Contact b)
        {
            if ((((object)a) == null) && (((object)b) != null)) return false;
            if ((((object)a) != null) && (((object)b) == null)) return false;
            if ((((object)a) == null) && (((object)b) == null)) return true;

            return a.ID == b.ID;
        }

        public static bool operator !=(Contact a, Contact b)
        {
            if ((((object)a) == null) && (((object)b) != null)) return true;
            if ((((object)a) != null) && (((object)b) == null)) return true;
            if ((((object)a) == null) && (((object)b) == null)) return false;

            return !(a.ID == b.ID);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Contact)) return false;

            return this == (Contact)obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
