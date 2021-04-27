using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LUC.DiscoveryService.Messages
{
    public class TcpMessage : Message
    {
        public TcpMessage(Int32 receivedProcolVersion, ConcurrentDictionary<String, List<String>> groupsSupported)
        {
            GroupsSupported = groupsSupported;
            VersionOfProtocol = receivedProcolVersion;
        }

        public TcpMessage(ConcurrentDictionary<String, List<String>> groupsSupported)
        {
            GroupsSupported = groupsSupported;
        }

        internal ConcurrentDictionary<String, List<String>> GroupsSupported { get; }
    }
}
