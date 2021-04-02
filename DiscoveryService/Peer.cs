using DeviceId;
using DiscoveryServices.Extensions;
using DiscoveryServices.Extensions.IPExtensions;
using System;
using System.Collections.Generic;
using System.Net;

namespace DiscoveryServices
{
    [Serializable]
    class Peer
    {
        private const Int32 MinValuePort = 17500;
        private const Int32 MaxValuePort = 17510;

        private const Int32 ProtocolVersion = 1;

        private Int32 runningPort;

        public Int32 VersionOfProtocol => ProtocolVersion;

        internal String Id { get; }

        internal List<IPAddress> IpAddresses { get; }

        internal List<String> GroupsSupported { get; }

        internal Int32 RunningPort
        {
            get => runningPort;
            set
            {
                if (value < MinValuePort || MaxValuePort < value)
                {
                    runningPort = MinValuePort;
                }
                else
                {
                    runningPort = value;
                }
            }
        }

        internal Peer(List<String> groupsSupported)
        {
            DeviceIdBuilder deviceIdBuilder = new DeviceIdBuilder();
            Id = deviceIdBuilder.GetMachineId();

            IpAddresses = Local_IP.GetLocalIPAddresses();
            GroupsSupported = groupsSupported;
            runningPort = MinValuePort;
        }
    }
}
