using LUC.DiscoveryService.Extensions;
using LUC.DiscoveryService.Kademlia.Interfaces;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    // ==========================

    class TcpProtocol : IProtocol
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(1);

        //private readonly ConnectionPool<T> connectionPool;

        //private static int REQUEST_TIMEOUT = 500;       // 500 ms for response.

        //public TcpProtocol(IEqualityComparer<T> comparerEndPoint)
        //{
        //    //connectionPool = new ConnectionPool<T>(poolMaxSize: 100, comparerEndPoint);
        //}

        /// <inheritdoc/>
        public RpcError Ping(Contact sender, EndPoint endPointOfClient)
        {
            var id = ID.RandomID;

            ErrorResponse peerError = new ErrorResponse();
            PingRequest request = new PingRequest
            {
                RandomID = id.Value,
                Sender = sender.ID.Value
            };
            PingResponse response = null;

            //GetClient(sender.ID, endPointOfClient, out var client, out var isInPool, out var isConnected);
            Boolean timeout;
            if(isConnected)
            {
                try
                {
                    ClientStart(isInPool, client, sender.ID, sender.EndPoint, request, out response);
                }
                catch (Exception ex)
                {
                    peerError.ErrorMessage = ex.Message;
                }
                //finally
                //{
                //    client.Shutdown(SocketShutdown.Both);
                //    client.Close();
                //}

                timeout = response == null;
            }
            else
            {
                timeout = false;
            }
            
            return GetRpcError(id, response, timeout, peerError);
        }

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {

            ErrorResponse peerError = new ErrorResponse();
            ID id = ID.RandomID;
            var request = new StoreRequest
            {
                IdOfSendingContact = sender.ID.Value,
                Key = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
                RandomID = id.Value,
                MessageOperation = MessageOperation.Store
            };

            //GetClient(sender.ID, endPointOfClient, out var client, out var isInPool, out var isConnected);
            Boolean timeout;
            StoreResponse response = null;
            if (isConnected)
            {
                try
                {
                    ClientStart(isInPool, client, sender.ID, sender.EndPoint, request, out response);
                }
                catch (Exception ex)
                {
                    peerError.ErrorMessage = ex.Message;
                }
                //finally
                //{
                //    client.Shutdown(SocketShutdown.Both);
                //    client.Close();
                //}

                timeout = response == null;
            }
            else
            {
                timeout = false;
            }

            return GetRpcError(id, response, timeout, peerError);
        }

        private void GetClient(ID idOfClient, T endPointOfClient, out DiscoveryServiceSocket client, out Boolean isInPool, out Boolean isConnected)
        {
            connectionPool.TakeClient(idOfClient.Value, endPointOfClient, ConnectTimeout, out client, out isConnected, out isInPool);
            if (client == null)
            {
                client = new DiscoveryServiceSocket(endPointOfClient.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
        }

        private void ClientStart<TResponse>(Boolean isInPool, DiscoveryServiceSocket client, ID idOfClient, T endPointOfClient, Request request, out TResponse response)
            where TResponse: Response
        {
            Boolean isConnected = client.Connected;
            if(!isInPool)
            {
                connectionPool.PutSocketInPool(client, idOfClient.Value, endPointOfClient, ConnectTimeout, out _, out _, out isConnected);
            }
            response = null;

            if (isConnected)
            {

                client.Send(request.ToByteArray(), SendTimeout, out var isSent);

                if (isSent)
                {
                    var bytesOfResponse = client.Receive(ReceiveTimeout, out var isReceived);
                    if (isReceived)
                    {
                        response.Read(bytesOfResponse);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, IPAddress host, UInt32 tcpPort)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError = false;
            DiscoveryServiceSocket client = new DiscoveryServiceSocket(host.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var request = new FindNodeRequest
            {
                Sender = sender.ID.Value,
                Key = keyToFindContacts.Value,
                RandomID = id.Value,
                MessageOperation = MessageOperation.FindNode
            };
            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContacts);
            FindNodeResponse response = null;
            GetClient(idOfClient: , , out var client);
            try
            {
                ClientStart(client, remoteContact.EndPoint, request, out response);
                
                //get close contacts near key
                var closeContacts = response.Contacts;

                return (closeContacts ?? EmptyContactList(), GetRpcError(id, response, timeoutError, peerError: null));
            }
            catch (SocketException ex)
            {
                error = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = true;

                return (EmptyContactList(), GetRpcError(id, null, timeoutError, error));
            }
            finally
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID keyToFindContact)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError = false;
            DiscoveryServiceSocket client = new DiscoveryServiceSocket(sender.IPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var request = new FindValueRequest
            {
                IdOfContact = keyToFindContact.Value,
                IdOfSendingContact = sender.ID.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id.Value,
            };
            
            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContact);
            FindValueResponse response = null;

            try
            {
                ClientStart(client, remoteContact.IPAddress, remoteContact.TcpPort, request, out response);

                //get close contacts near key
                var closeContacts = response?.CloseContactsToRepsonsingPeer/*.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList()*/;

                // Return only contacts with supported protocols.
                //return (contacts?.Where(c => c.Protocol != null).ToList(), ret.Value, GetRpcError(id, ret, timeoutError, error));

                return (closeContacts ?? EmptyContactList(), response.ValueInResponsingPeer, GetRpcError(id, response, timeoutError, peerError: null));
            }
            catch (SocketException ex)
            {
                error = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = true;

                return (EmptyContactList(), val: null, GetRpcError(id, null, timeoutError, error));
            }
            finally
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }

        protected RpcError GetRpcError(ID id, Response resp, bool timeoutError, ErrorResponse peerError)
        {
            RpcError rpcError = new RpcError
            {
                TimeoutError = timeoutError,
                PeerError = peerError != null,
                PeerErrorMessage = peerError?.ErrorMessage
            };

            if(resp == null)
            {
                rpcError.IDMismatchError = false;
            }
            else
            {
                rpcError.IDMismatchError = id != resp.RandomID;
            }

            return rpcError;
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }
    }
}
