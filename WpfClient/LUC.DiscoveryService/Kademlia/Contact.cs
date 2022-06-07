using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Linq;
using LUC.DiscoveryServices.Common;
using System.Text;
using LUC.Interfaces.Discoveries;
using LUC.Interfaces.Constants;
using System.Threading;

namespace LUC.DiscoveryServices.Kademlia
{
    internal class Contact : IContact
    {
        private readonly List<IPAddress> m_ipAddresses;

        private List<String> m_supportedBuckets;
        private Int32 m_tcpPort;
        private Int64 m_lastSeenInTicks;
        private KademliaId m_kadId;
        private IPAddress m_lastActiveIpAddress;

        /// <summary>
        /// Initialize a contact with its ID. Use this constructor when you don't know IP-addresses of your PC
        /// </summary>
        public Contact(String machineId, KademliaId contactID, UInt16 tcpPort, IEnumerable<String> bucketLocalNames)
        {
            MachineId = machineId;
            KadId = contactID;

            TcpPort = tcpPort;
            m_ipAddresses = new List<IPAddress>();

            InitBucketLocalNames(bucketLocalNames);

            Touch();
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(String machineId, KademliaId contactID, UInt16 tcpPort, IPAddress lastActiveIpAddress, IEnumerable<String> bucketLocalNames)
            : this(machineId, contactID, tcpPort, bucketLocalNames)
        {
            LastActiveIpAddress = lastActiveIpAddress;
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact(String machineId, KademliaId contactID, UInt16 tcpPort, IEnumerable<IPAddress> ipAddresses, DateTime lastSeen, IEnumerable<String> bucketLocalNames)
        {
            MachineId = machineId;
            KadId = contactID;

            TcpPort = tcpPort;

            m_ipAddresses = new List<IPAddress>();
            if ( ipAddresses != null )
            {
                TryAddIpAddressRange(ipAddresses);
            }

            InitBucketLocalNames(bucketLocalNames);

            LastSeen = lastSeen;
        }

        protected Contact()
        {
            ;//do nothing
        }

        public DateTime LastSeen
        {
            get => new DateTime(m_lastSeenInTicks);
            private set => Interlocked.Exchange(ref m_lastSeenInTicks, value.Ticks);
        }

        public String MachineId { get; }

        /// <summary>
        /// It shoud have public method set, because if user restart app, it will have new <see cref="KadId"/>, 
        /// but the same <see cref="MachineId"/> and it can still be in some <seealso cref="KBucket"/>, 
        /// so it should have the opportunity to update <see cref="KadId"/>
        /// </summary>
        public KademliaId KadId
        {
            get => m_kadId;
            set => Interlocked.Exchange(ref m_kadId, value);
        }

        public UInt16 TcpPort
        {
            get => (UInt16)m_tcpPort;
            set
            {
                AbstractDsData.CheckTcpPort(value);
                Interlocked.Exchange(ref m_tcpPort, value);
            }
        }

        public IPAddress LastActiveIpAddress
        {
            get => m_lastActiveIpAddress;
            set
            {
                Interlocked.Exchange(ref m_lastActiveIpAddress, value);

                TryAddIpAddress(value, isAdded: out _);
            }
        }

        public Int32 IpAddressesCount =>
            m_ipAddresses.Count;

        public override Boolean Equals( Object obj ) =>
            obj is IContact contact && Equals( contact );

        //public Boolean Equals(IContact a, IContact b) =>
        //    IsEqual(a, b);

        public Boolean Equals( IContact contact )
        {
            Boolean isContactNull = contact is null;

            Boolean isEqual = !isContactNull && MachineId.Equals( contact.MachineId, StringComparison.Ordinal );
            return isEqual;
        }

        public override Int32 GetHashCode() =>
            MachineId.GetHashCode();

        public override String ToString()
        {
            String contactAsStrWithoutAddresses = Display.ToString(this);

            var stringBuilder = new StringBuilder(contactAsStrWithoutAddresses);

            stringBuilder.AppendLine($"{Display.TABULATION}{nameof(m_ipAddresses)}:");

            lock ( m_ipAddresses )
            {
                for ( Int32 numAddress = 0; numAddress < IpAddressesCount; numAddress++ )
                {
                    if ( numAddress == IpAddressesCount - 1 )
                    {
                        stringBuilder.Append( $"{Display.TABULATION}{Display.TABULATION}{m_ipAddresses[ numAddress ]}" );
                    }
                    else
                    {
                        stringBuilder.AppendLine( $"{Display.TABULATION}{Display.TABULATION}{m_ipAddresses[ numAddress ]};" );
                    }
                }
            }

            return stringBuilder.ToString();
        }

        public void UpdateAccordingToNewState(IContact contactWithNewState)
        {
            LastActiveIpAddress = contactWithNewState.LastActiveIpAddress;

            //contact can restart program and it will have new ID,
            //so we need to update it in bucket
            KadId = contactWithNewState.KadId;

            //contact can have new enumerables of buckets and IP-addresses
            ExchangeLocalBucketRange(contactWithNewState.Buckets());
            TryAddIpAddressRange(contactWithNewState.IpAddresses());

            Touch();
        }

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Touch() =>
            LastSeen = DateTime.UtcNow;

        /// <returns>
        /// Copy of IP-addresses of contact
        /// </returns>
        public List<IPAddress> IpAddresses()
        {
            lock (m_ipAddresses)
            {
                return m_ipAddresses.ToList();
            }
        }

        /// <returns>
        /// Copy of bucket local names of contact
        /// </returns>
        public List<String> Buckets()
        {
            lock (m_supportedBuckets)
            {
                return m_supportedBuckets.ToList();
            }
        }

        public void ClearAllLocalBuckets()
        {
            lock (m_supportedBuckets)
            {
                m_supportedBuckets.Clear();
            }
        }

        public void ExchangeLocalBucketRange(IEnumerable<String> newBuckets) =>
            ExchangeEnumerable(m_supportedBuckets, newBuckets);

        public void ExchangeIpAddressRange(IEnumerable<IPAddress> newIpAddresses) =>
            ExchangeEnumerable(m_ipAddresses, newIpAddresses);

        public void TryAddIpAddressRange(IEnumerable<IPAddress> ipAddresses)
        {
            if ((ipAddresses != null) && ipAddresses.Any())
            {
                lock (m_ipAddresses)
                {
                    foreach (IPAddress address in ipAddresses)
                    {
                        //to put in the end last active IP address
                        if (m_ipAddresses.Contains(address))
                        {
                            m_ipAddresses.Remove(address);
                        }

                        AddNewIpAddresss(address);
                    }
                }
            }
        }

        public void TryAddIpAddress(IPAddress address, out Boolean isAdded)
        {
            if (address != null)
            {
                lock (m_ipAddresses)
                {
                    //to put in the end last active IP address
                    if (m_ipAddresses.Contains(address))
                    {
                        m_ipAddresses.Remove(address);
                    }

                    AddNewIpAddresss(address);
                    isAdded = true;
                }
            }
            else
            {
                isAdded = false;
            }
        }

        public void TryRemoveIpAddress(IPAddress address, out Boolean isRemoved)
        {
            lock (m_ipAddresses)
            {
                if (m_ipAddresses.Contains(address))
                {
                    isRemoved = m_ipAddresses.Remove(address);

                    if (m_ipAddresses.Count > 0)
                    {
                        Interlocked.Exchange(ref m_lastActiveIpAddress, value: m_ipAddresses[IpAddressesCount - 1]);
                    }
                    else if (m_ipAddresses.Count == 0)
                    {
                        Interlocked.Exchange(ref m_lastActiveIpAddress, null);
                    }
                }
                else
                {
                    isRemoved = false;
                }
            }
        }

        public void TryAddBucketLocalName(String bucketLocalName, out Boolean isAdded)
        {
            if (bucketLocalName != null)
            {
                lock (m_supportedBuckets)
                {
                    if (!m_supportedBuckets.Contains(bucketLocalName))
                    {
                        m_supportedBuckets.Add(bucketLocalName);
                        isAdded = true;
                    }
                    else
                    {
                        isAdded = false;
                    }
                }
            }
            else
            {
                isAdded = false;
            }
        }

        public void TryRemoveBucketLocalName(String bucketLocalName, out Boolean isRemoved)
        {
            lock (m_supportedBuckets)
            {
                isRemoved = m_supportedBuckets.Contains(bucketLocalName) && m_supportedBuckets.Remove(bucketLocalName);
            }
        }

        private static Boolean IsEqual(IContact a, IContact b)
        {
            Boolean isEqual = false;

            Boolean isANull = a is null;
            Boolean isBNull = b is null;

            if (!isANull && !isBNull)
            {
                isEqual = a.Equals(b);
            }
            else if (isANull && isBNull)
            {
                isEqual = true;
            }
            else if ((isANull && !isBNull) || (!isANull && isBNull))
            {
                isEqual = false;
            }

            return isEqual;
        }

        private void InitBucketLocalNames(IEnumerable<String> bucketLocalNames)
        {
            m_supportedBuckets = new List<String>();
            if ( bucketLocalNames != null )
            {
                ExchangeLocalBucketRange(bucketLocalNames);
            }
        }

        private void AddNewIpAddresss(IPAddress address)
        {
            m_ipAddresses.Add(address);

            m_lastActiveIpAddress = address;

            if ( m_ipAddresses.Count > DsConstants.MAX_AVAILABLE_IP_ADDRESSES_IN_CONTACT )
            {
                //remove the oldest IP-address
                m_ipAddresses.RemoveAt(index: 0);
            }
        }

        private void ExchangeEnumerable<T>(List<T> oldEnumerable, IEnumerable<T> newEnumerable)
        {
            if (newEnumerable != null)
            {
                lock (oldEnumerable)
                {
                    oldEnumerable.Clear();
                    oldEnumerable.AddRange(newEnumerable);
                }
            }
        }

        //public static Boolean operator ==( Contact a, Contact b ) =>
        //    IsEqual( a, b );

        //public static Boolean operator !=( Contact a, Contact b ) =>
        //    !( a == b );
    }
}
