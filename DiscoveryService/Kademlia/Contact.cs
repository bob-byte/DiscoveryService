//using LUC.DiscoveryService.Kademlia.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace LUC.DiscoveryService.Kademlia
{
    public class Contact : IComparable
    {
        private Int32 lastIndexOfAddress;
        private readonly Dictionary<Int32, IPAddress> local_IpAddresses = new Dictionary<Int32, IPAddress>();

        // For serialization.  Don't want to use JsonConstructor because we don't want to touch the LastSeen.
        public Contact()
        {
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(ID contactID, UInt16 tcpPort, IPAddress locaIpAddresses)
        {
            ID = contactID;
            TcpPort = tcpPort;
            LastActiveIpAddress = locaIpAddresses;

            Touch();
        }

        public DateTime LastSeen { get; set; }

        public ID ID { get; set; }

        public UInt16 TcpPort { get; set; }

        public IPAddress LastActiveIpAddress { get; set; }

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            LastSeen = DateTime.Now;
        }

        public void TryAddIpAddress(IPAddress address, out Boolean isAdded)
        {
            lock(local_IpAddresses)
            {
                isAdded = !local_IpAddresses.ContainsValue(address);

                if(isAdded)
                {
                    local_IpAddresses.Add(++lastIndexOfAddress, address);
                }
            }
        }

        public IPAddress IpAddress(Int32 index)
        {
            lock(local_IpAddresses)
            {
                return local_IpAddresses[index];
            }
        }

        public void TryRemoveIpAddress(IPAddress address, out Boolean isRemoved)
        {
            lock(local_IpAddresses)
            {
                if(local_IpAddresses.ContainsValue(address))
                {
                    var idOfAddress = local_IpAddresses.Single(c => c.Value.Equals(address)).Key;
                    isRemoved = local_IpAddresses.Remove(idOfAddress);
                }
                else
                {
                    isRemoved = false;
                }
            }
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
                   $"{nameof(local_IpAddresses)} = {local_IpAddresses};\n" +
                   $"{nameof(LastSeen)} = {LastSeen};\n";
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
