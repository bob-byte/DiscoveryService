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
        public Int32 VersionOfProtocol => ProtocolVersion;

        private String id;
        internal String Id => id;

        private List<IPAddress> ipAddresses;
        internal List<IPAddress> IpAddresses => ipAddresses;

        private List<String> groupsSupported;
        internal List<String> GroupsSupported => groupsSupported;

        private Int32 runningPort;
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
            Lock.InitWithLock(Lock.lockId, deviceIdBuilder.GetDeviceId(), ref id);
            Lock.InitWithLock(Lock.lockIp, Local_IP.GetLocalIPAddresses(), ref ipAddresses);
            Lock.InitWithLock(Lock.lockGroupsSupported, groupsSupported, ref this.groupsSupported);

            Int32? nullablePort = null;
            Lock.InitWithLock(Lock.lockPort, MinValuePort, ref nullablePort);
            runningPort = (Int32)nullablePort;
        }
    }
}
