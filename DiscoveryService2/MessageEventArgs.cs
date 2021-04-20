using System;
using System.Net;

namespace LUC.DiscoveryService
{
    /// <summary>
    ///   The event data for <see cref="MulticastService.QueryReceived"/> or
    ///   <see cref="MulticastService.AnswerReceived"/>.
    /// </summary>
    public class MessageEventArgs : EventArgs
    {
        /// <summary>
        ///   The LightUpon.Cloud message.
        /// </summary>
        /// <value>
        ///   The received message.
        /// </value>
        public Message Message { get; set; }

        /// <summary>
        ///   Message sender endpoint.
        /// </summary>
        /// <value>
        ///   The endpoint from the message was received.
        /// </value>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        ///   Protocol version.
        /// </summary>
        /// <value>
        ///   Integer.
        /// </value>
	public ushort ProtocolVersion;

        /// <summary>
        ///   User Groups with their SSL certificates.
	///   SSL should have SNI ( Server Name Indication ) feature enabled
	///   This allows us to tell which group we are trying to connect to, so that the server knows which certificate to use.
	///
	///   We generate SSL and key/certificate pairs for every group. These are distributed from server to user’s computers 
	///   which are authenticated for the buckets later.
	///
	///   These are rotated any time membership changes e.g., when someone is removed from a group/shared folder. 
	///   We can require both ends of the HTTPS connection to authenticate with the same certificate (the certificate for the group).
	///   This proves that both ends of the connection are authenticated.
        /// </summary>
        /// <value>
        ///   Some unique value.
        /// </value>
        private Dictionary<String, String> Groups; // stores group: SSL certificate
    }
}
