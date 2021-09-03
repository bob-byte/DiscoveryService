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
        private static ILoggingService log;
        private readonly UInt16 protocolVersion;

        static ClientKadOperation()
        {
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        //public ClientKadOperation()
        //{
        //    ;//do nothing
        //}

        public ClientKadOperation(UInt16 protocolVersion)
        {
            this.protocolVersion = protocolVersion;
        }

        /// <inheritdoc/>
        public RpcError Ping(Contact sender, Contact remoteContact)
        {
            var id = ID.RandomID;
            PingRequest request = new PingRequest
            {
                Sender = sender.ID.Value,
            };

            request.GetResult<PingResponse>(remoteContact, protocolVersion, response: out _, out var rpcError);
            return rpcError;
        }

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, Contact remoteContact, bool isCached = false, int expirationTimeSec = 0)
        {
            var request = new StoreRequest
            {
                Sender = sender.ID.Value,
                KeyToStore = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
            };

            //var remoteContact = RemoteContact(key);
            request.GetResult<StoreResponse>(remoteContact, protocolVersion, response: out _, out var rpcError);
            return rpcError;
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, Contact remoteContact)
        {
            var id = ID.RandomID.Value;
            var request = new FindNodeRequest
            {
                Sender = sender.ID.Value,
                KeyToFindCloseContacts = keyToFindContacts.Value,
            };
            request.GetResult<FindNodeResponse>(remoteContact, protocolVersion, out var response, out var rpcError);

            return (response?.CloseSenderContacts?.ToList() ?? EmptyContactList(), rpcError);
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID keyToFindContact, Contact remoteContact)
        {
            var id = ID.RandomID.Value;

            var request = new FindValueRequest
            {
                KeyToFindCloseContacts = keyToFindContact.Value,
                Sender = sender.ID.Value,
            };

            request.GetResult<FindValueResponse>(remoteContact, protocolVersion, out var response, out var rpcError);
            var closeContacts = response?.CloseContacts?.ToList() ?? EmptyContactList();

            return (closeContacts, response?.ValueInResponsingPeer, rpcError);
        }
    }
}
