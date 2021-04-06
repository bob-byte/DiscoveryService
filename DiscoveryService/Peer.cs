using DeviceId;
using DiscoveryServices.Extensions;
using DiscoveryServices.Extensions.IPExtensions;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace DiscoveryServices
{
    class Peer
    {
        private readonly Int32 minValuePort, maxValuePort;
        private Int32 runningPort;

        internal String Id { get; }

        internal IPAddress IpAddress { get; }

        internal List<String> GroupsSupported { get; }

        public X509Certificate Certificate { get; }

        internal Dictionary<String, IPAddress> KnownOtherPeers { get; }

        internal Int32 RunningPort
        {
            get => runningPort;
            set
            {
                if (value < minValuePort || maxValuePort < value)
                {
                    runningPort = minValuePort;
                }
                else
                {
                    runningPort = value;
                }
            }
        }

        internal Peer(List<String> groupsSupported, X509Certificate certificate, Int32 minValuePort, Int32 maxValuePort)
        {
            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            Id = deviceIdBuilder.GetMachineId();

            IpAddress = Local_IP.GetLocalIPAddress(System.Net.Sockets.AddressFamily.InterNetwork);
            GroupsSupported = groupsSupported;
            Certificate = certificate;

            runningPort = minValuePort;
            this.minValuePort = minValuePort;
            this.maxValuePort = maxValuePort;
        }
    }
}
