using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Defines a specific service for sending multicast messages.
    ///
    ///   Service Profiles allow us to support several protocol versions at the same time.
    /// </summary>
    /// <seealso cref="ServiceDiscovery.Advertise(ServiceProfile)"/>
    public class ServiceProfile
    {
        // Enforce multicast defaults, especially TTL.
        static ServiceProfile()
        {
            // Make sure MulticastService is inited.
            MulticastService.ReferenceEquals(null, null);
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        public ServiceProfile()
        {
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class
        ///   with the specified details.
        /// </summary>
        /// <param name="instanceName">
        ///    A unique machine identifier for the specific service instance.
        /// </param>
        /// <param name="serviceName">
        ///   The <see cref="ServiceName">name</see> of the service.
        /// </param>
        /// <param name="port">
        ///   The UDP port to send multicast messages to.
        /// </param>
        /// <param name="addresses">
        ///   The IP addresses of the specific service instance. If <b>null</b> then
        ///   <see cref="MulticastService.GetIPAddresses"/> is used.
        /// </param>
        public ServiceProfile(ushort protocolVersion, string machineId, ushort tcpPortStart, tcpPortEnd,
			      Dictionary<String, String> groups, IEnumerable<IPAddress> addresses = null)
        {
	    ProtocolVersion = protocolVersion;
	    MachineId = machineId;
	    TcpPortStart = tcpPortStart;
	    TcpPortEnd = tcpPortEnd;
	    Groups = groups;

            foreach (var address in addresses ?? MulticastService.GetLinkLocalAddresses())
            {
		// TODO: to add network interfaces to the list
                NetworkInterfaces.Add(address);
            }
        }

        /// <summary>
        ///   Protocol version.
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
	public ushort ProtocolVersion;

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; set; }

	// Define TCP port range to listen to
        public ushort TcpPortStart;
        public ushort TcpPortEnd;

        /// <summary>
        ///   User Groups with their SSL certificates.
	///   SSL should have SNI ( Server Name Indication ) feature enabled
	///   This allows us to tell which group we are trying to connect to, so that the server knows which certificate to use.
	///
	///   We generate SSL and key/certificate pairs for every group. These are distributed from server to user’s computers 
	///   which are authenticated for the buckets later.
	///
	///   These are rotated any time membership changes e.g., when someone is removed from a group/shared folder. 
	///   We can require both ends of the HTTPS connection to authenticate with the same certificate (the certificate for the group).
	///   This proves that both ends of the connection are authenticated.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        private Dictionary<String, String> Groups; // stores group: SSL certificate
    }
}
