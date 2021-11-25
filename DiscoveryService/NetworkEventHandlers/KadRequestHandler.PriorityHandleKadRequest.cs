using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.NetworkEventHandlers
{
    partial class KadOperationRequestHandler
    {
        /// <summary>
        /// Contains cases of handling kademlia requests
        /// </summary>
        private enum PriorityHandleKadRequest
        {
            //Unset value
            None,

            /// <summary>
            /// First send response to a request, later execute kademlia server operation
            /// </summary>
            FirstSendResponse,

            /// <summary>
            /// First execute kademlia server operation, later send response to a request
            /// </summary>
            FirstExecuteKadServerOp
        }
    }
}
