//using LUC.DiscoveryService.Kademlia.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Collections;

namespace LUC.DiscoveryService.Kademlia
{
    public class Contact : IComparable, IEnumerable<IPAddress>
    {
        private readonly Object lockIpAddresses;
        private readonly List<IPAddress> ipAddresses = new List<IPAddress>();
        //private ManualResetEvent isUsedByKadOp = new ManualResetEvent(initialState: );

        // For serialization.  Don't want to use JsonConstructor because we don't want to touch the LastSeen.
        public Contact()
        {
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(ID contactID, UInt16 tcpPort, IPAddress lastActiveIpAddress)
        {
            ID = contactID;
            TcpPort = tcpPort;
            LastActiveIpAddress = lastActiveIpAddress;
            lockIpAddresses = new Object();

            Touch();
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(ID contactID, UInt16 tcpPort, IEnumerable<IPAddress> ipAddresses)
            : this(contactID, tcpPort, lastActiveIpAddress: ipAddresses.Last())
        {
            this.ipAddresses = ipAddresses.ToList();
        }

        public DateTime LastSeen { get; set; }

        public ID ID { get; set; }

        public UInt16 TcpPort { get; set; }

        public IPAddress LastActiveIpAddress { get; set; }

        public Int32 IpAddressesCount => ipAddresses.Count;

        public IPAddress this[Int32 index] => ipAddresses[index];

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch()
        {
            LastSeen = DateTime.Now;
        }

        public List<IPAddress> IpAddresses() =>
            ipAddresses.ToList();

        public void TryAddIpAddress(IPAddress address, out Boolean isAdded)
        {
            lock(lockIpAddresses)
            {
                isAdded = !ipAddresses.Contains(address);

                if(isAdded)
                {
                    ipAddresses.Add(address);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() =>
            ipAddresses.GetEnumerator();

        public IEnumerator<IPAddress> GetEnumerator() =>
            ipAddresses.GetEnumerator();

        //public IPAddress IpAddress(Int32 index)
        //{
        //    lock(local_IpAddresses)
        //    {
        //        return local_IpAddresses[index];
        //    }
        //}

        public void TryRemoveIpAddress(IPAddress address, out Boolean isRemoved)
        {
            lock(lockIpAddresses)
            {
                if(ipAddresses.Contains(address))
                {
                    isRemoved = ipAddresses.Remove(address);
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
                   $"{nameof(ipAddresses)} = {ipAddresses};\n" +
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
