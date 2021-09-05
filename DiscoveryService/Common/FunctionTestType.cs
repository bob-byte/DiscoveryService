using LUC.DiscoveryService.Kademlia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    /// <summary>
    /// For functional tests
    /// </summary>
    class FunctionTestType
    {
        /// <summary>
        /// Defines whether current PC can receive TCP message from itself
        /// </summary>
        public static Boolean CanCurrentPcToReceiveTcpMessFromItself(ID ourContactId, ID remoteContactId) =>
            ourContactId == remoteContactId;
    }
}
