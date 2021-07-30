//using LUC.DiscoveryService.Kademlia.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Collections;
using System.IO;

namespace LUC.DiscoveryService.Kademlia
{
    public class Contact : IComparable, IEnumerable<IPAddress>
    {
        private readonly Object lockIpAddresses;
        private readonly List<IPAddress> ipAddresses;
        private IPAddress lastActiveIpAddress;

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(ID contactID, UInt16 tcpPort)
        {
            ID = contactID;
            TcpPort = tcpPort;
            ipAddresses = new List<IPAddress>();
            lockIpAddresses = new Object();

            Touch();
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(ID contactID, UInt16 tcpPort, IPAddress lastActiveIpAddress)
            : this(contactID, tcpPort)
        {
            LastActiveIpAddress = lastActiveIpAddress;
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(ID contactID, UInt16 tcpPort, IEnumerable<IPAddress> ipAddresses, DateTime lastSeen)
        {
            ID = contactID;
            TcpPort = tcpPort;
            lockIpAddresses = new Object();

            if(ipAddresses?.Count() >= 1)
            {
                this.ipAddresses = ipAddresses.ToList();
                LastActiveIpAddress = ipAddresses.Last();
            }
            else
            {
                this.ipAddresses = new List<IPAddress>();
            }
            
            LastSeen = lastSeen;
        }

        public DateTime LastSeen { get; set; }

        public ID ID { get; }

        public UInt16 TcpPort { get; set; }

        public IPAddress LastActiveIpAddress 
        {
            get => lastActiveIpAddress;
            set
            {
                lastActiveIpAddress = value;
                TryAddIpAddress(lastActiveIpAddress, out _);
            }
        }

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

        public void TryRemoveIpAddress(IPAddress address, out Boolean isRemoved)
        {
            lock(lockIpAddresses)
            {
                if(ipAddresses.Contains(address))
                {
                    isRemoved = ipAddresses.Remove(address);

                    if(ipAddresses.Count == 0)
                    {
                        lastActiveIpAddress = null;
                    }
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
            using(StringWriter writer = new StringWriter())
            {
                writer.WriteLine($"{PropertyWithValue(nameof(ID), ID)};\n" +
                                 $"{PropertyWithValue(nameof(LastSeen), LastSeen)};");

                writer.WriteLine($"{nameof(ipAddresses)}:");
                for (Int32 numAddress = 0; numAddress < IpAddressesCount; numAddress++)
                {
                    if (numAddress == IpAddressesCount - 1)
                    {
                        writer.Write($"{ipAddresses[numAddress]}");
                    }
                    else
                    {
                        writer.WriteLine($"{ipAddresses[numAddress]};");
                    }
                }

                return writer.ToString();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected String PropertyWithValue<T>(String nameProp, T value) =>
            $"{nameProp} = {value}";

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
