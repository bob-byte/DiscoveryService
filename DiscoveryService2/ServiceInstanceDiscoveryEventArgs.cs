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
        ///   Version of protocol the remote side supports.
        /// </summary>
	ushort protocolVersion { get; set; }
	public List<string> ServiceInstanceGroups { get; set; }
    }
}
