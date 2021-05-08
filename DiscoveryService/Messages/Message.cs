using Makaretu.Dns;
using System;

namespace LUC.DiscoveryService.Messages
{
    public abstract class Message
    {
        public const UInt32 ProtocolVersion = 1;

        public Message()
        {

        }

        public Message(UInt32 messageId)
        {
            MessageId = messageId;
        }

        public UInt32 MessageId { get; set; }
        public UInt32 VersionOfProtocol { get; set; }
        public MessageStatus Status { get; set; }
    }
}