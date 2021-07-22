using LUC.DiscoveryService.Kademlia;
using LUC.Interfaces;
using LUC.Services.Implementation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    /// <summary>
    /// Contains basic info about a Service Discovery
    /// </summary>
    public abstract class AbstractService
    {
        /// <summary>
        /// Default port which is used in LAN
        /// </summary>
        public const UInt16 DefaultPort = 17500;

        /// <summary>
        /// Count available ports which the LAN supports
        /// </summary>
        public const UInt16 CountAvailablePorts = 10;

        protected UInt16 runningTcpPort;

        [Import(typeof(ILoggingService))]
        protected static readonly LoggingService log = new LoggingService();

        public AbstractService()
        {
            log.SettingsService = new SettingsService();

            runningTcpPort = DefaultPort;
            MinValueTcpPort = DefaultPort;
            MaxValueTcpPort = DefaultPort + CountAvailablePorts;
            RunningUdpPort = DefaultPort;
        }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv4 protocol.
        /// </summary>
        public Boolean UseIpv4 { get; protected set; }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv6 protocol.
        /// </summary>
        public Boolean UseIpv6 { get; protected set; }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; protected set; }

        /// <summary>
        /// Min available TCP port in the LAN
        /// </summary>
        public UInt16 MinValueTcpPort { get; }

        /// <summary>
        /// Max available TCP port in the LAN
        /// </summary>
        public UInt16 MaxValueTcpPort { get; }

        /// <summary>
        /// TCP port which current peer is using in TCP connections
        /// </summary>
        public UInt16 RunningTcpPort
        {
            get => runningTcpPort;
            protected set
            {
                runningTcpPort = (MinValueTcpPort <= value) && (value <= MaxValueTcpPort) ?
                    value : MinValueTcpPort;
            }
        }

        /// <summary>
        /// UDP port which current peer is using in UDP connections
        /// </summary>
        public UInt16 RunningUdpPort { get; }

        /// <summary>
        ///   Protocol version.
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
        public UInt16 ProtocolVersion { get; protected set; }
    }
}
