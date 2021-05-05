using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LUC.DiscoveryService.Messages
{
    public class TcpMessage : Message
    {
        public TcpMessage(Int32 messageId, Int32 receivedProcolVersion, ConcurrentDictionary<String, List<String>> groupsSupported)
            : base(messageId)
        {
            GroupsSupported = groupsSupported;
            VersionOfProtocol = receivedProcolVersion;
        }

        public TcpMessage(Int32 messageId, ConcurrentDictionary<String, List<String>> groupsSupported) 
            : base(messageId)
        {
            GroupsSupported = groupsSupported;
        }

        public ConcurrentDictionary<String, List<String>> GroupsSupported { get; }
    }
}
