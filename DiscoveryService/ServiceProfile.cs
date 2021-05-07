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
        private readonly Int32 minValueTcpPort, maxValueTcpPort;
        private Int32 runningTcpPort;

        // Enforce multicast defaults, especially TTL.
        static ServiceProfile()
        {
            // Make sure MulticastService is inited.
            Service.ReferenceEquals(null, null);
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        /// <param name="addresses">
        /// <see cref="IPAddress"/> of network interfaces of current machine 
        /// </param>
        public ServiceProfile(Int32 minValueTcpPort, Int32 maxValueTcpPort, Int32 udpPort, 
            Int32 protocolVersion, ConcurrentDictionary<String, List<KeyValuePair<String, String>>> groupsSupported, 
            IEnumerable<IPAddress> addresses = null)
        {
            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            MachineId = deviceIdBuilder.GetMachineId();

            if(groupsSupported != null)
            {
                GroupsSupported = groupsSupported;
            }
            else
            {
                GroupsSupported = new ConcurrentDictionary<String, List<KeyValuePair<String, String>>>();
            }

            ProtocolVersion = protocolVersion;

            RunningUdpPort = udpPort;
            runningTcpPort = minValueTcpPort;
            this.minValueTcpPort = minValueTcpPort;
            this.maxValueTcpPort = maxValueTcpPort;
        }

        /// <summary>
        /// Handle using event. Don't return NetworkInterface.GetAllNetworkInterfaces()
        /// </summary>
        public ICollection<NetworkInterface> NetworkInterfaces => NetworkInterface.GetAllNetworkInterfaces();

        /// <summary>
        ///   Protocol version.
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
	    public Int32 ProtocolVersion { get; }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; set; }

        internal Int32 RunningTcpPort
        {
            get => runningTcpPort;
            set
            {
                if (value < minValueTcpPort || maxValueTcpPort < value)
                {
                    runningTcpPort = minValueTcpPort;
                }
                else
                {
                    runningTcpPort = value;
                }
            }
        }

        internal Int32 RunningUdpPort { get; }

        /// <summary>
        /// This property use in internal classes and allow to avoid strong connectivity. It is weaker, because we don't use object type DiscoveryService in the different classes
        /// </summary>
        public ConcurrentDictionary<String, List<KeyValuePair<String, String>>> GroupsSupported { get; }
    }
}
