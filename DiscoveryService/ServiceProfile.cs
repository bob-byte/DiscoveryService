
using DiscoveryServices.Common;
using LUC.Interfaces;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace DiscoveryServices
{
    /// <summary>
    ///   Contains info about current peer
    /// </summary>
    public class ServiceProfile : AbstractDsData
    {
        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        public ServiceProfile( String machineId, Boolean useIpv4, Boolean useIpv6, UInt16 protocolVersion,
            ConcurrentDictionary<String, String> groupsSupported = null )
        {
            MachineId = machineId;

            Boolean osSupportsIPv4 = Socket.OSSupportsIPv4;
            Boolean osSupportsIPv6 = Socket.OSSupportsIPv6;

            String baseLogRecord = $"Underlying OS or network adapters don't support";
            if ( useIpv4 && !osSupportsIPv4 )
            {
                useIpv4 = osSupportsIPv4;
                DsLoggerSet.DefaultLogger.LogFatal( message: $"{baseLogRecord} IPv4" );
            }

            if ( useIpv6 && !osSupportsIPv6 )
            {
                useIpv6 = osSupportsIPv6;
                DsLoggerSet.DefaultLogger.LogFatal( $"{baseLogRecord} IPv6" );
            }

            DefaultInit( useIpv4, useIpv6, protocolVersion, groupsSupported );
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="ServiceProfile"/> class.
        /// </summary>
        public ServiceProfile( String machineId, UInt16 protocolVersion, ConcurrentDictionary<String, String> groupsSupported = null )
        {
            MachineId = machineId;

            Boolean osSupportsIPv4 = Socket.OSSupportsIPv4;
            Boolean osSupportsIPv6 = Socket.OSSupportsIPv6;

            String baseLogRecord = "Doesn't support ";
            if ( !osSupportsIPv4 )
            {
                DsLoggerSet.DefaultLogger.LogFatal( message: $"{baseLogRecord} IPv4" );
            }

            if ( !osSupportsIPv6 )
            {
                DsLoggerSet.DefaultLogger.LogFatal( $"{baseLogRecord} IPv6" );
            }

            DefaultInit( osSupportsIPv4, osSupportsIPv6, protocolVersion, groupsSupported );
        }


        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }

        /// <summary>
        /// Known network interfaces
        /// </summary>
        public IList<NetworkInterface> KnownNetworkInterfaces() => 
            NetworkEventInvoker.AllTransmitableNetworkInterfaces().ToList();

        private void DefaultInit( Boolean useIpv4, Boolean useIpv6, UInt16 protocolVersion,
            ConcurrentDictionary<String, String> groupsSupported = null )
        {
            GroupsSupported = groupsSupported ?? new ConcurrentDictionary<String, String>();

            ProtocolVersion = protocolVersion;

            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;
        }
    }
}
