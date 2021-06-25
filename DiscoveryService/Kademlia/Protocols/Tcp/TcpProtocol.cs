using System;
using System.Collections.Generic;
using System.Linq;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    // ==========================

    public class TcpProtocol : IProtocol
    {
#if DEBUG       // for unit tests
        public bool Responds { get; set; }
#endif

        // For serialization:
        public string Url { get { return url; } set { url = value; } }
        public UInt32 Port { get { return port; } set { port = value; } }

        protected string url;
        protected UInt32 port;

        /// <summary>
        /// For serialization.
        /// </summary>
        public TcpProtocol()
        {
        }

        public TcpProtocol(string url, UInt32 port)
        {
            this.url = url;
            this.port = port;

#if DEBUG
            Responds = true;
#endif
        }

        /// <summary>
        /// This operation has two purposes:
        /// <list type="bullet">
        /// <item>
        /// A peer can issue this RPC(remote procedure call) on contacts it knows about, updating its own list of "close" peers
        /// </item>
        /// <item>
        /// A peer may issue this RPC to discover other peers on the network
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit ID near which you want to get list of contacts
        /// </param>
        /// <returns>
        /// <see cref="Constants.K"/> nodes the recipient of the RPC  knows about closest to the target <paramref name="key"/>. 
        /// Contacts can come from a single k-bucket, or they may come from multiple k-buckets if the closest k-bucket is not full. 
        /// In any case, the RPC recipient must return k items 
        /// (unless there are fewer than k nodes in all its k-buckets combined, in which case it returns every node it knows about).
        /// Also this operation returns <seealso cref="RpcError"/> to inform about the errors which maybe happened
        /// </returns>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID key)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindNodeResponse, ErrorResponse>(url + ":" + port + "//FindNode",
                new FindNodeSubnetRequest()
                {
                    Protocol = sender.Protocol,
                    ProtocolName = sender.Protocol.GetType().Name,
                    Sender = sender.ID.Value,
                    Key = key.Value,
                    RandomID = id.Value
                }, out error, out timeoutError);

            try
            {
                var contacts = ret?.Contacts?.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList();

                // Return only contacts with supported protocols.
                return (contacts?.Where(c => c.Protocol != null).ToList() ?? EmptyContactList(), GetRpcError(id, ret, timeoutError, error));
            }
            catch (Exception ex)
            {
                return (null, new RpcError() { ProtocolError = true, ProtocolErrorMessage = ex.Message });
            }
        }

        /// <summary>
        /// Attempt to find the value in the peer network. Also this operation has two purposes:
        /// <list type="bullet">
        /// <item>
        /// A peer can issue this RPC(remote procedure call) on contacts it knows about, updating its own list of "close" peers
        /// </item>
        /// <item>
        /// A peer may issue this RPC to discover other peers on the network
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit ID near which you want to get list of contacts
        /// </param>
        /// <returns>A null contact list is acceptable here as it is a valid return if the value is found.
        /// The caller is responsible for checking the timeoutError flag to make sure null contacts is not
        /// the result of a timeout error.
        /// Also this operation returns <seealso cref="RpcError"/> to inform about the errors which maybe happened
        /// </returns>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID key)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindValueResponse, ErrorResponse>(url + ":" + port + "//FindValue",
                new FindValueSubnetRequest()
                {
                    Protocol = sender.Protocol,
                    ProtocolName = sender.Protocol.GetType().Name,
                    Sender = sender.ID.Value,
                    Key = key.Value,
                    RandomID = id.Value
                }, out error, out timeoutError);

            try
            {
                var contacts = ret?.Contacts?.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList();

                // Return only contacts with supported protocols.
                return (contacts?.Where(c => c.Protocol != null).ToList(), ret.Value, GetRpcError(id, ret, timeoutError, error));
            }
            catch (Exception ex)
            {
                return (null, null, new RpcError() { ProtocolError = true, ProtocolErrorMessage = ex.Message });
            }
        }

        /// <summary>
        /// Someone is pinging us.  Register the contact and respond.
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <returns>
        /// Information about the errors which maybe happened
        /// </returns>
        public RpcError Ping(Contact sender)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindValueResponse, ErrorResponse>(url + ":" + port + "//Ping",
                new PingSubnetRequest()
                {
                    Protocol = sender.Protocol,
                    ProtocolName = sender.Protocol.GetType().Name,
                    Sender = sender.ID.Value,
                    RandomID = id.Value
                }, 
                out error, out timeoutError);

            return GetRpcError(id, ret, timeoutError, error);
        }

        /// <summary>
        /// Insturcts a node to store a (<paramref name="key"/>, <paramref name="val"/>) pair for later retrieval. 
        /// “To store a (<paramref name="key"/>, <paramref name="val"/>) pair, a participant locates the k closest nodes to the key and sends them STORE RPCS.” 
        /// The participant does this by inspecting its own k-closest nodes to the key.
        /// Store a key-value pair in the republish or cache storage.
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit <seealso cref="ID"/> which we want to store
        /// </param>
        /// <param name="val">
        /// Value of the peer which we want to store
        /// </param>
        /// <param name="isCached">
        /// Whether cach a (<paramref name="key"/>, <paramref name="val"/>) pair
        /// </param>
        /// <param name="expirationTimeSec">
        /// Time of expiration cach in seconds
        /// </param>
        /// <returns>
        /// Information about the errors which maybe happened
        /// </returns>
        public RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindValueResponse, ErrorResponse>(url + ":" + port + "//Store",
                    new StoreSubnetRequest()
                    {
                        Protocol = sender.Protocol,
                        ProtocolName = sender.Protocol.GetType().Name,
                        Sender = sender.ID.Value,
                        Key = key.Value,
                        Value = val,
                        IsCached = isCached,
                        ExpirationTimeSec = expirationTimeSec,
                        RandomID = id.Value
                    }, 
                    out error, out timeoutError);

            return GetRpcError(id, ret, timeoutError, error);
        }

        protected RpcError GetRpcError(ID id, BaseResponse resp, bool timeoutError, ErrorResponse peerError)
        {
            return new RpcError() { IDMismatchError = id != resp.RandomID, TimeoutError = timeoutError, PeerError = peerError != null, PeerErrorMessage = peerError?.ErrorMessage };
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }
    }
}
