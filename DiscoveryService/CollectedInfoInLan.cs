using System;
using System.Collections.Concurrent;

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
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }
    }
}
