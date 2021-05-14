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
    public class ServiceProfile
    {
        private readonly UInt32 minValueTcpPort, maxValueTcpPort;
        private UInt32 runningTcpPort;

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
        public ServiceProfile(UInt32 minValueTcpPort, UInt32 maxValueTcpPort, UInt32 udpPort, 
            UInt32 protocolVersion, ConcurrentDictionary<String, String> groupsSupported, 
            ConcurrentDictionary<String, String> knownIps, IEnumerable<IPAddress> addresses = null)
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

            if (knownIps != null)
            {
                KnownIps = knownIps;
            }
            else
            {
                KnownIps = new ConcurrentDictionary<String, String>();
            }

            ProtocolVersion = protocolVersion;

            RunningUdpPort = udpPort;
            runningTcpPort = minValueTcpPort;
            this.minValueTcpPort = minValueTcpPort;
            this.maxValueTcpPort = maxValueTcpPort;
        }

        /// <summary>
        /// Known network interfaces
        /// </summary>
        public ICollection<NetworkInterface> NetworkInterfaces => Service.KnownNics;

        /// <summary>
        ///   Protocol version.
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
	    public UInt32 ProtocolVersion { get; }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; set; }

        /// <summary>
        /// TCP port which current peer is using in TCP connections
        /// </summary>
        public UInt32 RunningTcpPort
        {
            get => runningTcpPort;
            set
            {
                runningTcpPort = (value < minValueTcpPort) || (maxValueTcpPort < value) ? 
                    minValueTcpPort : value;
            }
        }

        /// <summary>
        /// UDP port which current peer is using in UDP connections
        /// </summary>
        internal UInt32 RunningUdpPort { get; }

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        /// <remarks>
        /// This property is used in internal classes and allow to avoid strong connectivity. It is weaker, because we don't use object type <seealso cref="ServiceDiscovery"/>  in the different classes.
        /// </remarks>
        public ConcurrentDictionary<String, String> GroupsSupported { get; }

        /// <summary>
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IP-address:port"
        /// </summary>
        /// <remarks>
        /// This property is used in internal classes and allows to avoid strong connectivity. It is weaker, because we don't use object type <seealso cref="ServiceDiscovery"/> in the different classes.
        /// </remarks>
        public ConcurrentDictionary<String, String> KnownIps { get; }
    }
}
