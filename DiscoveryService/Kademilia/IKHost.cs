using System;
using System.Collections.Generic;

namespace Alethic.Kademlia
{

    /// <summary>
    /// Represents a Kademlia network host.
    /// This is the central "tying together" component. It's goal is to manage
    /// the activities related to incoming network requests, and determine their
    /// responses. It also provides a user-visible API surface for high-level Kademlia operations.
    /// </summary>
    /// <typeparam name="TNodeId"></typeparam>
    public interface IKHost<TNodeId>
        where TNodeId : unmanaged
    {

        /// <summary>
        /// Unique identifier of the network.
        /// </summary>
        ulong NetworkId { get; }

        /// <summary>
        /// Gets the Node ID of the node itself.
        /// </summary>
        TNodeId SelfId { get; }

        /// <summary>
        /// Gets the set of endpoints of the node.
        /// </summary>
        IReadOnlyCollection<Uri> Endpoints { get; }

        /// <summary>
        /// Registers an endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        bool RegisterEndpoint(Uri endpoint);

        /// <summary>
        /// Unregisters an endpoint.
        /// </summary>
        /// <param name="endpoint"></param>
        bool UnregisterEndpoint(Uri endpoint);

        /// <summary>
        /// Raised when the endpoints are changed.
        /// </summary>
        event EventHandler EndpointsChanged;

        /// <summary>
        /// Resolves the protocol endpoint from the given URI.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        IKProtocolEndpoint<TNodeId> ResolveEndpoint(Uri uri);

        /// <summary>
        /// Gets the set of endpoints of the node.
        /// </summary>
        IReadOnlyCollection<IKProtocol<TNodeId>> Protocols { get; }

        /// <summary>
        /// Registers an protocol.
        /// </summary>
        /// <param name="protocol"></param>
        bool RegisterProtocol(IKProtocol<TNodeId> protocol);

        /// <summary>
        /// Unregisters an protocol.
        /// </summary>
        /// <param name="protocol"></param>
        bool UnregisterProtocol(IKProtocol<TNodeId> protocol);

        /// <summary>
        /// Raised when the protocols are changed.
        /// </summary>
        event EventHandler ProtocolsChanged;

    }

}
