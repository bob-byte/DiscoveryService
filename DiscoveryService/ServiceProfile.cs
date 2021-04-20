﻿using DeviceId;
using LUC.DiscoveryService.Extensions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   Contains info about current peer
    /// </summary>
    /// <seealso cref="ServiceDiscovery.Advertise(ServiceProfile)"/>
    class ServiceProfile
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
        public ServiceProfile(Int32 minValueTcpPort, Int32 maxValueTcpPort, Int32 udpPort, Int32 protocolVersion, X509Certificate certificate, Dictionary<EndPoint, List<X509Certificate>> groupsSupported)
        {
            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            MachineId = deviceIdBuilder.GetMachineId();

            if(groupsSupported != null)
            {
                GroupsSupported = groupsSupported;
            }
            else
            {
                GroupsSupported = new Dictionary<EndPoint, List<X509Certificate>>();
            }
            Certificate = certificate;

            ProtocolVersion = protocolVersion;

            RunningUdpPort = udpPort;
            runningTcpPort = minValueTcpPort;
            this.minValueTcpPort = minValueTcpPort;
            this.maxValueTcpPort = maxValueTcpPort;
        }

        /// <summary>
        /// Why do we need this property?
        /// </summary>
        public ICollection<IPAddress> NetworkInterfaces { get; }

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
        public Dictionary<EndPoint, List<X509Certificate>> GroupsSupported { get; }

        /// <summary>
        /// <see cref="X509Certificate"/> is basic of all certificates for SSL in .NET
        /// </summary>
        public X509Certificate Certificate { get; set; }
    }
}