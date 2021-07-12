using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Messages
{
    public abstract class KademliaMessage : Message
    {
        /// <summary>
        ///   The kind of message.
        /// </summary>
        public MessageOperation MessageOperation { get; set; }
    }
}
