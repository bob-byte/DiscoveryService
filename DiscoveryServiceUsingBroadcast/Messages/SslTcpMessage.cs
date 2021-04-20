using System;
using System.Collections.Generic;

namespace DiscoveryServices.Messages
{
    class SslTcpMessage : Message
    {
        internal List<String> GroupsSupported { get; }


        public SslTcpMessage(Int32 receivedProcolVersion, List<String> groupsSupported)
        {
            GroupsSupported = groupsSupported;
            VersionOfProtocol = receivedProcolVersion;
        }

        public SslTcpMessage(List<String> groupsSupported)
        {
            GroupsSupported = groupsSupported;
        }
    }
}
