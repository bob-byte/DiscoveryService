using Makaretu.Dns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages
{
    public abstract class Message
    {
        public const Int32 ProtocolVersion = 1;

        public Int32 VersionOfProtocol { get; set; }
        public MessageStatus Status { get; set; }
    }
}
