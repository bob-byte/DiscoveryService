namespace DiscoveryServices.NetworkEventHandlers
{
    partial class KadRpcHandler
    {
        /// <summary>
        /// Contains cases of handling kademlia requests
        /// </summary>
        private enum PriorityHandleKadRpc
        {
            //Unset value
            None,

            /// <summary>
            /// First send response to a request, later execute kademlia server procedure call
            /// </summary>
            FirstSendResponse,

            /// <summary>
            /// First execute kademlia server procedure call, later send response to a request
            /// </summary>
            FirstExecuteProcedureCall
        }
    }
}
