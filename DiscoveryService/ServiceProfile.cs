using DeviceId;
using LUC.DiscoveryService.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Contains info about current peer
    /// </summary>
    public class ServiceProfile : CollectedInfoInLan
    {
        static ServiceProfile()
        {
            // Make sure Service is inited.
            Service.ReferenceEquals(null, null);
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        /// <param name="addresses">
        /// <see cref="IPAddress"/> of network interfaces of current machine 
        /// </param>
        public ServiceProfile(Boolean useIpv4, Boolean useIpv6, UInt32 protocolVersion, 
            ConcurrentDictionary<String, String> groupsSupported = null, 
            IEnumerable<IPAddress> addresses = null)
        {
            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            MachineId = deviceIdBuilder.MachineId();

            if(groupsSupported != null)
            {
                GroupsSupported = groupsSupported;
            }
            else
            {
                GroupsSupported = new ConcurrentDictionary<String, String>();
            }

            ProtocolVersion = protocolVersion;

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
        }

        /// <summary>
        /// Known network interfaces
        /// </summary>
        public ICollection<NetworkInterface> NetworkInterfaces => Service.KnownNics;
    }
}
