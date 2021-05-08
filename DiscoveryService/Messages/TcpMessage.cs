using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LUC.DiscoveryService.Messages
{
    public class TcpMessage : Message
    {
        public TcpMessage()
        {

        }

        public TcpMessage(UInt32 messageId, UInt32 receivedProcolVersion, ConcurrentDictionary<String, String> groupsSupported, ConcurrentDictionary<String, String> knownIps)
            : base(messageId)
        {
            GroupsSupported = groupsSupported;
            KnownIps = knownIps;
            VersionOfProtocol = receivedProcolVersion;
        }

        public TcpMessage(UInt32 messageId, ConcurrentDictionary<String, String> groupsSupported, ConcurrentDictionary<String, String> knownIps) 
            : base(messageId)
        {
            GroupsSupported = groupsSupported;
            KnownIps = knownIps;
            VersionOfProtocol = ProtocolVersion;
        }

        public ConcurrentDictionary<String, String> GroupsSupported { get; set; }

        public ConcurrentDictionary<String, String> KnownIps { get; set; }
    }
}
