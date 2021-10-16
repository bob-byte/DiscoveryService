using DeviceId;

using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Kademlia;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Contains info about current peer
    /// </summary>
    public class ServiceProfile : AbstractService
    {
        static ServiceProfile()
        {
            // Make sure NetworkEventInvoker is inited. This row causes initialization of all static members of NetworkEventInvoker
            NetworkEventInvoker.ReferenceEquals( null, null );
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        public ServiceProfile( Boolean useIpv4, Boolean useIpv6, UInt16 protocolVersion,
            ConcurrentDictionary<String, String> groupsSupported = null )
        {
            LUC.DiscoveryService.MachineId.Create(out String machineId);
            MachineId = machineId;

            GroupsSupported = groupsSupported ?? new ConcurrentDictionary<String, String>();

            ProtocolVersion = protocolVersion;

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
        }

        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }

        /// <summary>
        /// Known network interfaces
        /// </summary>
        public IList<NetworkInterface> KnownNetworkInterfaces => NetworkEventInvoker.NetworkInterfaces().ToList();
    }
}
