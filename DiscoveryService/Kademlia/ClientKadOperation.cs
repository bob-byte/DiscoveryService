using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia
{
    class ClientKadOperation
    {
        private readonly UInt16 m_protocolVersion;

        public ClientKadOperation( UInt16 protocolVersion )
        {
            this.m_protocolVersion = protocolVersion;
        }

        /// <inheritdoc/>
        public RpcError Ping( Contact sender, Contact remoteContact )
        {
            PingRequest request = new PingRequest( sender.ID.Value );

            request.GetResult<PingResponse>( remoteContact, m_protocolVersion, response: out _, out RpcError rpcError );
            return rpcError;
        }

        ///<inheritdoc/>
        public RpcError Store( Contact sender, KademliaId key, String val, Contact remoteContact, Boolean isCached = false, Int32 expirationTimeSec = 0 )
        {
            StoreRequest request = new StoreRequest( sender.ID.Value )
            {
                KeyToStore = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
            };

            request.GetResult<StoreResponse>( remoteContact, m_protocolVersion, response: out _, out RpcError rpcError );
            return rpcError;
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode( Contact sender, KademliaId keyToFindContacts, Contact remoteContact )
        {
            FindNodeRequest request = new FindNodeRequest( sender.ID.Value )
            {
                KeyToFindCloseContacts = keyToFindContacts.Value,
            };
            request.GetResult<FindNodeResponse>( remoteContact, m_protocolVersion, out FindNodeResponse response, out RpcError rpcError );

            return (response?.CloseSenderContacts?.ToList() ?? EmptyContactList(), rpcError);
        }

        protected List<Contact> EmptyContactList() => new List<Contact>();

        /// <inheritdoc/>
        public (List<Contact> contacts, String val, RpcError error) FindValue( Contact sender, KademliaId keyToFindContact, Contact remoteContact )
        {
            FindValueRequest request = new FindValueRequest( sender.ID.Value )
            {
                KeyToFindCloseContacts = keyToFindContact.Value,
            };

            request.GetResult<FindValueResponse>( remoteContact, m_protocolVersion, out FindValueResponse response, out RpcError rpcError );
            List<Contact> closeContacts = response?.CloseContacts?.ToList() ?? EmptyContactList();

            return (closeContacts, response?.ValueInResponsingPeer, rpcError);
        }
    }
}
