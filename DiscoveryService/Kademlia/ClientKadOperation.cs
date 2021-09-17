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
            m_protocolVersion = protocolVersion;
        }

        /// <inheritdoc/>
        public RpcError Ping( Contact sender, Contact remoteContact )
        {
            PingRequest request = new PingRequest( sender.KadId.Value );

            request.GetResult<PingResponse>( remoteContact, m_protocolVersion, response: out _, out RpcError rpcError );
            return rpcError;
        }

        ///<inheritdoc/>
        public RpcError Store( Contact sender, KademliaId key, String val, Contact remoteContact, Boolean isCached = false, Int32 expirationTimeSec = 0 )
        {
            StoreRequest request = new StoreRequest( sender.KadId.Value )
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
            FindNodeRequest request = new FindNodeRequest( sender.KadId.Value )
            {
                KeyToFindCloseContacts = keyToFindContacts.Value,
            };
            request.GetResult( remoteContact, m_protocolVersion, out FindNodeResponse response, out RpcError rpcError );
            List<Contact> closeContacts = response?.CloseSenderContacts?.ToList() ?? EmptyContactList();

            return (closeContacts, rpcError);
        }

        protected List<Contact> EmptyContactList() => new List<Contact>();

        /// <inheritdoc/>
        public (List<Contact> contacts, String val, RpcError error) FindValue( Contact sender, KademliaId keyToFindContact, Contact remoteContact )
        {
            FindValueRequest request = new FindValueRequest( sender.KadId.Value )
            {
                KeyToFindCloseContacts = keyToFindContact.Value,
            };

            request.GetResult( remoteContact, m_protocolVersion, out FindValueResponse response, out RpcError rpcError );
            List<Contact> closeContacts = response?.CloseContacts?.ToList() ?? EmptyContactList();

            return (closeContacts, response?.ValueInResponsingPeer, rpcError);
        }
    }
}
