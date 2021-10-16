using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using LUC.DiscoveryService.Kademlia.ClientPool;

namespace LUC.DiscoveryService.Common
{
    class ThrowConcurrencyException
    {
        /// <summary>
        /// Display also stack trace where created <paramref name="exception"/>
        /// </summary>
        /// <param name="exception">
        /// To show stack race
        /// </param>
        public static void ThrowWithConnectionPoolSocketDescr( ConnectionPoolSocket socket )
        {
            InvalidOperationException concurrencyException = new InvalidOperationException( message: $"Socket with id {socket.Id} is in usage by another thread:\n" );
            throw concurrencyException;
        }
    }
}
