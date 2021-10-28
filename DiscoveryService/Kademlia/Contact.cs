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
using LUC.Interfaces.Models;

namespace LUC.DiscoveryService.Kademlia
{
    public class Contact : IComparable
    {
        private readonly Object m_lockIpAddresses;

        private readonly List<IPAddress> m_ipAddresses;
        private List<String> m_supportedBuckets;

        private IPAddress m_lastActiveIpAddress;

        /// <summary>
        /// Initialize a contact with its ID. Use this constructor when you want to initialize contact of current PC first time
        /// </summary>
        public Contact( String machineId, KademliaId contactID, UInt16 tcpPort, IEnumerable<String> bucketLocalNames )
        {
            MachineId = machineId;
            KadId = contactID;

            TcpPort = tcpPort;
            m_ipAddresses = new List<IPAddress>();
            m_lockIpAddresses = new Object();

            InitBucketLocalNames( bucketLocalNames );

            Touch();
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact( String machineId, KademliaId contactID, UInt16 tcpPort, IPAddress lastActiveIpAddress, IEnumerable<String> bucketLocalNames )
            : this( machineId, contactID, tcpPort, bucketLocalNames )
        {
            LastActiveIpAddress = lastActiveIpAddress;
        }

        /// <summary>
        /// Initialize a contact with its ID.
        /// </summary>
        public Contact( String machineId, KademliaId contactID, UInt16 tcpPort, IEnumerable<IPAddress> ipAddresses, DateTime lastSeen, IEnumerable<String> bucketLocalNames )
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

            InitBucketLocalNames( bucketLocalNames );

            LastSeen = lastSeen;
        }

        public DateTime LastSeen { get; private set; }

        public String MachineId { get; }

        /// <summary>
        /// It shoud have public method set, because if node disapears 
        /// from network and returns there, it will have new <see cref="KadId"/>, 
        /// but the same <see cref="MachineId"/> and it can still be in some bucket, 
        /// so it should have the opportunity to update <see cref="KadId"/>
        /// </summary>
        public KademliaId KadId { get; set; }

        public UInt16 TcpPort { get; }

        public IPAddress LastActiveIpAddress
        {
            get => m_lastActiveIpAddress;
            set
            {
                m_lastActiveIpAddress = value;

                TryAddIpAddress( value, out _ );
            }
        }

        public Int32 IpAddressesCount => m_ipAddresses.Count;

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public void Touch() => 
            LastSeen = DateTime.UtcNow;

        /// <returns>
        /// Copy of IP-addresses of contact
        /// </returns>
        public List<IPAddress> IpAddresses() =>
            m_ipAddresses.ToList();

        /// <returns>
        /// Copy of bucket local names of contact
        /// </returns>
        public List<String> SupportedBuckets() =>
            m_supportedBuckets.ToList();

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

        public void TryAddBucketLocalName(String bucketLocalName, out Boolean isAdded)
        {
            if ( bucketLocalName != null )
            {
                lock ( m_supportedBuckets )
                {
                    if ( !m_supportedBuckets.Contains( bucketLocalName ) )
                    {
                        m_supportedBuckets.Add( bucketLocalName );
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

        public void TryRemoveBucketLocalName( String bucketLocalName, out Boolean isRemoved )
        {
            lock(m_supportedBuckets)
            {
                isRemoved = m_supportedBuckets.Contains( bucketLocalName ) ? m_supportedBuckets.Remove( bucketLocalName ) : false;
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

                writer.WriteLine( $"{Display.TABULATION}{nameof( m_ipAddresses )}:" );
                for ( Int32 numAddress = 0; numAddress < IpAddressesCount; numAddress++ )
                {
                    if ( numAddress == IpAddressesCount - 1 )
                    {
                        writer.Write( $"{Display.TABULATION}{Display.TABULATION}{m_ipAddresses[ numAddress ]}" );
                    }
                    else
                    {
                        writer.WriteLine( $"{Display.TABULATION}{Display.TABULATION}{m_ipAddresses[ numAddress ]};" );
                    }
                }

                return writer.ToString();
            }
        }

        public static Boolean operator ==( Contact a, Contact b )
        {
            Boolean isEqual = false;

            if( ( ( (Object)a ) != null ) && ( ( (Object)b ) != null ) )
            {
                isEqual = a.MachineId == b.MachineId;
            }
            else if ( ( ( (Object)a ) == null ) && ( ( (Object)b ) == null ) )
            {
                isEqual = true;
            }
            else if ( ( ( (Object)a ) == null ) && ( ( (Object)b ) != null ) )
            {
                isEqual = false;
            }
            else if ( ( ( (Object)a ) != null ) && ( ( (Object)b ) == null ) )
            {
                isEqual = false;
            }

            return isEqual;
        }

        public static Boolean operator !=( Contact a, Contact b ) =>
            !( a == b );

        public override Boolean Equals( Object obj )
        {
            Boolean isEqual;

            //if obj is null it also hasn't Contact type
            if(obj is Contact contact)
            {
                isEqual = this == contact;
            }
            else
            {
                isEqual = false;
            }

            return isEqual;
        }

        public override Int32 GetHashCode() => 
            MachineId.GetHashCode();

        private void InitBucketLocalNames(IEnumerable<String> bucketLocalNames)
        {
            m_supportedBuckets = new List<String>();
            if ( bucketLocalNames != null )
            {
                foreach ( var bucketName in bucketLocalNames )
                {
                    TryAddBucketLocalName( bucketName, isAdded: out _ );
                }
            }
        }

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
