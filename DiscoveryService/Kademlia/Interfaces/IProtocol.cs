using LUC.DiscoveryService.Messages.KademliaRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Interfaces
{
    public interface IProtocol
    {
        /// <summary>
        /// Pings contact with <paramref name="host"/> and <paramref name="tcpPort"/>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <returns>
        /// Information about the errors which maybe happened
        /// </returns>
        RpcError Ping(Contact sender, IPAddress host, Int32 tcpPort);

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
        (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID key, IPAddress host, Int32 tcpPort);

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
        (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID key);

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
        RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0);
    }
}
