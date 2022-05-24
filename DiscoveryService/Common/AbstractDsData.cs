using LUC.Interfaces.Constants;

using System;

namespace DiscoveryServices.Common
{
    /// <summary>
    /// Contains a basic info about a Service Discovery
    /// </summary>
    public abstract class AbstractDsData
    {
        /// <summary>
        /// Min available TCP port in the LAN
        /// </summary>
        private const UInt16 MIN_VALUE_TCP_PORT = DsConstants.DEFAULT_PORT;

        /// <summary>
        /// Max available TCP port in the LAN
        /// </summary>
        private const UInt16 MAX_VALUE_TCP_PORT = DsConstants.DEFAULT_PORT + COUNT_AVAILABLE_PORTS;

        /// <summary>
        /// The number of available ports on network interfaces
        /// </summary>
        private const UInt16 COUNT_AVAILABLE_PORTS = 10;

        protected UInt16 m_runningTcpPort;

        protected AbstractDsData()
        {
            m_runningTcpPort = DsConstants.DEFAULT_PORT;
            RunningUdpPort = DsConstants.DEFAULT_PORT;
        }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv4 protocol.
        /// </summary>
        public virtual Boolean UseIpv4 { get; protected set; }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv6 protocol.
        /// </summary>
        public virtual Boolean UseIpv6 { get; protected set; }

        /// <summary>
        ///   A unique identifier for the service instance.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        public String MachineId { get; protected set; }

        /// <summary>
        /// TCP port which current peer is using in TCP connections. 
        /// If you want to set more than <see cref="MAX_VALUE_TCP_PORT"/>, 
        /// this property will be equal to <see cref="MIN_VALUE_TCP_PORT"/> 
        /// in order to <see cref="DiscoveryService"/> can normal increment this property. If we set <see cref="MAX_VALUE_TCP_PORT"/> in this situation, <see cref="DiscoveryService"/> will have errors, because it wants to change port to work normally
        /// </summary>
        public UInt16 RunningTcpPort
        {
            get => m_runningTcpPort;
            protected set
            {
                Boolean isInPortRange = IsInPortRange( tcpPort: value );
                m_runningTcpPort = isInPortRange ? value : MIN_VALUE_TCP_PORT;
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
        internal static Boolean IsInPortRange( Int32? tcpPort ) =>
            ( MIN_VALUE_TCP_PORT <= tcpPort ) && ( tcpPort <= MAX_VALUE_TCP_PORT );

        internal static void CheckTcpPort(Int32? tcpPort)
        {
            Boolean isInPortRange = IsInPortRange( tcpPort );
            if ( !isInPortRange )
            {
                throw new ArgumentException( message: $"{tcpPort} is not in port range" );
            }
        }
    }
}
