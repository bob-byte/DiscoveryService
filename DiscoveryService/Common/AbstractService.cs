using LUC.DiscoveryServices.Kademlia;
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

namespace LUC.DiscoveryServices.Common
{
    /// <summary>
    /// Contains a basic info about a Service Discovery
    /// </summary>
    public abstract class AbstractService
    {
        protected UInt16 m_runningTcpPort;

        /// <summary>
        /// Default port which is used in LAN
        /// </summary>
        public const UInt16 DEFAULT_PORT = 17500;

        /// <summary>
        /// The number of available ports on network interfaces
        /// </summary>
        public const UInt16 COUNT_AVAILABLE_PORTS = 10;

        static AbstractService()
        {
            SettingsService = new SettingsService();
            LoggingService = new LoggingService
            {
                SettingsService = SettingsService
            };
        }

        public AbstractService()
        {
            m_runningTcpPort = DEFAULT_PORT;
            MinValueTcpPort = DEFAULT_PORT;
            MaxValueTcpPort = DEFAULT_PORT + COUNT_AVAILABLE_PORTS;
            RunningUdpPort = DEFAULT_PORT;
        }

        [Import( typeof( ILoggingService ) )]
        public static ILoggingService LoggingService { get; set; } = new LoggingService();

        [Import( typeof( ILoggingService ) )]
        public static ISettingsService SettingsService { get; set; } = new SettingsService();

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
        /// TCP port which current peer is using in TCP connections. 
        /// If you want to set more than <see cref="MaxValueTcpPort"/>, 
        /// this property will be equal to <see cref="MinValueTcpPort"/> 
        /// in order to <see cref="DiscoveryService"/> can normal increment this property. If we set <see cref="MaxValueTcpPort"/> in this situation, <see cref="DiscoveryService"/> will have errors, because it wants to change port to work normally
        /// </summary>
        public UInt16 RunningTcpPort
        {
            get => m_runningTcpPort;
            protected set
            {
                Boolean isInPortRange = IsInPortRange( tcpPort: value );
                m_runningTcpPort = isInPortRange ? value : MinValueTcpPort;
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

        /// <summary>
        ///   Returns true if provided port value is in range between MinValueTcpPort and MaxValueTcpPort.
        /// </summary>
        protected Boolean IsInPortRange( Int32? tcpPort ) =>
            ( MinValueTcpPort <= tcpPort ) && ( tcpPort <= MaxValueTcpPort );
    }
}
