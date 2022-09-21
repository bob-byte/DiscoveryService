using LUC.DiscoveryServices.Common;

using System;
using System.Threading;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    partial class ConnectionPool
    {
        /// <summary>
        /// Client socket, maintained by the Connection Pool
        /// </summary>
        partial class Socket
        {
            /// <summary>
            /// We need that status in pool was reference type, because when we 
            /// create a new socket to update connection (see <seealso cref="NewSimilarSocket(SocketStateInPool)"/>), 
            /// we also need to update status in pool of the old socket that exists there
            /// </summary>
            private sealed class StateInPoolReference
            {
                //it is Int32, because we need to use Intelocked.Exchange for faster thread-safe execution 
                private Int32 m_value;

                public StateInPoolReference( SocketStateInPool value )
                {
                    m_value = (Int32)value;
                }

                public SocketStateInPool Value
                {
                    get => (SocketStateInPool)m_value;
                    set => Interlocked.Exchange( ref m_value, (Int32)value );
                }
            }
        }
    }    
}
