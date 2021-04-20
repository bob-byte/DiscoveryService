using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Text;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   The event data for <see cref="ServiceDiscovery.ServiceInstanceDiscovered"/>.
    /// </summary>
    public class ServiceInstanceDiscoveryEventArgs : MessageEventArgs
    {
        /// <summary>
        ///   The IP address of the service instance.
        /// </summary>
        /// <value>
        ///   Typically the IP belongs to one of local networks: 10.0.0.0,
	//    172.16.0.0 or 192.168.0.0.
        /// </value>
        public IPAddress ServiceInstanceIp { get; set; }
	public List<string> ServiceInstanceGroups { get; set; }
    }
}

