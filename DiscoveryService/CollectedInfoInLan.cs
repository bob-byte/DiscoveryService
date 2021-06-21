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
            TcpPort = DefaultPort;
        }

        /// <summary>
        /// TCP port, that we send to other computers.
        /// </summary>
        public UInt32 TcpPort { get; protected set; }

        /// <summary>
        /// Groups which current peer supports.
        /// Key is a name of group, which current peer supports.
        /// Value is a SSL certificate of group
        /// </summary>
        public ConcurrentDictionary<String, String> GroupsSupported { get; protected set; }
    }
}
