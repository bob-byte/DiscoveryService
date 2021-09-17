//using LUC.DiscoveryService.Kademlia.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Collections;
using System.IO;
using LUC.DiscoveryService.Kademlia.Exceptions;
using LUC.DiscoveryService.Common;

namespace LUC.DiscoveryService.Kademlia
{
    public class Contact : IComparable
    {
        private readonly Object m_lockIpAddresses;
        private readonly List<IPAddress> m_ipAddresses;
        private IPAddress m_lastActiveIpAddress;

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact( String machineId, KademliaId contactID, UInt16 tcpPort )
        {
            MachineId = machineId;
            KadId = contactID;

            TcpPort = tcpPort;
            m_ipAddresses = new List<IPAddress>();
            m_lockIpAddresses = new Object();

            Touch();
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact( String machineId, KademliaId contactID, UInt16 tcpPort, IPAddress lastActiveIpAddress )
            : this( machineId, contactID, tcpPort )
        {
            LastActiveIpAddress = lastActiveIpAddress;
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact( String machineId, KademliaId contactID, UInt16 tcpPort, IEnumerable<IPAddress> ipAddresses, DateTime lastSeen )
        {
            MachineId = machineId;
            KadId = contactID;

            TcpPort = tcpPort;
            m_lockIpAddresses = new Object();

            m_ipAddresses = new List<IPAddress>();
            if ( ipAddresses != null )
            {
                foreach ( IPAddress address in ipAddresses )
                {
                    TryAddIpAddress( address, isAdded: out _ );
                }
            }

            LastSeen = lastSeen;
        }

        public DateTime LastSeen { get; set; }

        public String MachineId { get; set; }

        public KademliaId KadId { get; set; }

        public UInt16 TcpPort { get; set; }

        public IPAddress LastActiveIpAddress
        {
            get => m_lastActiveIpAddress;
            set
            {
                m_lastActiveIpAddress = value;
                TryAddIpAddress( m_lastActiveIpAddress, out _ );
            }
        }

        public Int32 IpAddressesCount => m_ipAddresses.Count;

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Touch() => 
            LastSeen = DateTime.UtcNow;

        public List<IPAddress> IpAddresses() =>
            m_ipAddresses.ToList();

        public void TryAddIpAddress( IPAddress address, out Boolean isAdded )
        {
            if ( address != null )
            {
                lock ( m_lockIpAddresses )
                {
                    //to put in the end last active IP address
                    if ( m_ipAddresses.Contains( address ) )
                    {
                        m_ipAddresses.Remove( address );
                    }

                    AddNewIpAddresss( address );
                    isAdded = true;
                }
            }
            else
            {
                isAdded = false;
            }
        }

        public void TryRemoveIpAddress( IPAddress address, out Boolean isRemoved )
        {
            lock ( m_lockIpAddresses )
            {
                if ( m_ipAddresses.Contains( address ) )
                {
                    isRemoved = m_ipAddresses.Remove( address );

                    if ( m_ipAddresses.Count > 0 )
                    {
                        m_lastActiveIpAddress = m_ipAddresses[ IpAddressesCount - 1 ];
                    }
                    else if ( m_ipAddresses.Count == 0 )
                    {
                        m_lastActiveIpAddress = null;
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

        public Int32 CompareTo( Object obj )
        {
            Validate.IsTrue<NotContactException>( obj is Contact, "Cannot compare non-Contact objects to a Contact" );

            Contact c = (Contact)obj;

            return KadId.CompareTo( c.KadId );
        }

        public override String ToString()
        {
            using ( StringWriter writer = new StringWriter() )
            {
                writer.WriteLine( $"{Display.PropertyWithValue( nameof( KadId ), KadId )};\n" +
                                 $"{Display.PropertyWithValue( nameof( LastSeen ), LastSeen )};" );

                writer.WriteLine( $"{nameof( m_ipAddresses )}:" );
                for ( Int32 numAddress = 0; numAddress < IpAddressesCount; numAddress++ )
                {
                    if ( numAddress == IpAddressesCount - 1 )
                    {
                        writer.Write( $"{m_ipAddresses[ numAddress ]}" );
                    }
                    else
                    {
                        writer.WriteLine( $"{m_ipAddresses[ numAddress ]};" );
                    }
                }

                return writer.ToString();
            }
        }

        public static Boolean operator ==( Contact a, Contact b )
        {
            if ( ( ( (Object)a ) == null ) && ( ( (Object)b ) != null ) )
                return false;
            if ( ( ( (Object)a ) != null ) && ( ( (Object)b ) == null ) )
                return false;
            if ( ( ( (Object)a ) == null ) && ( ( (Object)b ) == null ) )
                return true;

            return a.KadId == b.KadId;
        }

        public static Boolean operator !=( Contact a, Contact b )
        {
            if ( ( ( (Object)a ) == null ) && ( ( (Object)b ) != null ) )
                return true;
            if ( ( ( (Object)a ) != null ) && ( ( (Object)b ) == null ) )
                return true;
            if ( ( ( (Object)a ) == null ) && ( ( (Object)b ) == null ) )
                return false;

            return !( a.KadId == b.KadId );
        }

        public override Boolean Equals( Object obj )
        {
            if ( obj == null || !( obj is Contact ) )
                return false;

            return this == (Contact)obj;
        }

        public override Int32 GetHashCode() => base.GetHashCode();

        private void AddNewIpAddresss( IPAddress address )
        {
            m_ipAddresses.Add( address );

            m_lastActiveIpAddress = address;

            if ( m_ipAddresses.Count > Constants.MAX_AVAILABLE_IP_ADDRESSES_IN_CONTACT )
            {
                //remove the oldest IP-address
                m_ipAddresses.RemoveAt( index: 0 );
            }
        }
    }
}
