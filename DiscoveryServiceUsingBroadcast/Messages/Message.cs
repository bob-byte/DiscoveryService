using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices.Messages
{
    class Message
    {
        public const Int32 ProtocolVersion = 1;

        public Int32 VersionOfProtocol { get; set; }
    }
}
