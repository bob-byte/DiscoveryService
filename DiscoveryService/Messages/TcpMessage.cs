using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace LUC.DiscoveryService.Messages
{
    class TcpMessage : Message
    {
        public TcpMessage(Int32 receivedProcolVersion, Dictionary<EndPoint, List<X509Certificate>> groupsSupported)
        {
            GroupsSupported = groupsSupported;
            VersionOfProtocol = receivedProcolVersion;
        }

        public TcpMessage(Dictionary<EndPoint, List<X509Certificate>> groupsSupported)
        {
            GroupsSupported = groupsSupported;
        }

        internal Dictionary<EndPoint, List<X509Certificate>> GroupsSupported { get; }
    }
}
