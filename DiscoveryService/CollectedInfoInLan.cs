using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    /// <summary>
    /// Contains info about LAN
    /// </summary>
    public abstract class CollectedInfoInLan : AbstractService
    {
        public CollectedInfoInLan()
        {
            KadPort = DefaultPort;
        }

        /// <summary>
        /// Kademilia port, that we send to other computers.
        /// </summary>
        public UInt32 KadPort { get; protected set; }

        /// <summary>
        /// IP address of groups which were discovered.
        /// Key is a name of group, which current peer supports.
        /// Value is a network in a format "IP-address:port"
        /// </summary>
        public ConcurrentDictionary<String, String> KnownIps { get; protected set; }

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }
    }
}
