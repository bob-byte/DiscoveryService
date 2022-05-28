using System;
using System.Collections.Generic;
using System.Net;

namespace LUC.Interfaces.Discoveries
{
    public interface IContact
    {
        String MachineId { get; }

        /// <summary>
        /// It shoud have public method set, because if node disapears 
        /// from network and returns there, it will have new <see cref="KadId"/>, 
        /// but the same <see cref="MachineId"/> and it can still be in some bucket, so
        /// we will need to update it there
        /// </summary>
        KademliaId KadId { get; set; }

        UInt16 TcpPort { get; set; }

        IPAddress LastActiveIpAddress { get; set; }

        Int32 IpAddressesCount { get; }

        DateTime LastSeen { get; }

        void UpdateAccordingToNewState( IContact contactWithNewState );

        /// <summary>
        /// Update the fact that we've just seen this contact.
        /// </summary>
        void Touch();

        List<IPAddress> IpAddresses();

        List<String> Buckets();

        void ClearAllLocalBuckets();

        void TryAddIpAddress( IPAddress address, out Boolean isAdded );

        void TryAddIpAddressRange( IEnumerable<IPAddress> ipAddresses );

        void TryRemoveIpAddress( IPAddress address, out Boolean isRemoved );

        void TryAddBucketLocalName( String bucketLocalName, out Boolean isAdded );

        void ExchangeLocalBucketRange( IEnumerable<String> newBuckets );

        void ExchangeIpAddressRange( IEnumerable<IPAddress> newIpAddresses );

        void TryRemoveBucketLocalName( String bucketLocalName, out Boolean isRemoved );
    }
}
