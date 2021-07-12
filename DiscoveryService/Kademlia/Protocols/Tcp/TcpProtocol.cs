using LUC.DiscoveryService.Extensions;
using LUC.DiscoveryService.Kademlia.Interfaces;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
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
        //private readonly ConnectionPool<T> connectionPool;

        //private static int REQUEST_TIMEOUT = 500;       // 500 ms for response.

        //public TcpProtocol(IEqualityComparer<T> comparerEndPoint)
        //{
        //    //connectionPool = new ConnectionPool<T>(poolMaxSize: 100, comparerEndPoint);
        //}

        private ILoggingService loggingService;


        public TcpProtocol(ILoggingService loggingService)
        {
            this.loggingService = loggingService;
            connectionPool = ConnectionPool.Instance(loggingService);
        }

        private ConnectionPool connectionPool;

        /// <inheritdoc/>
        public RpcError Ping(Contact sender, IPAddress host, Int32 tcpPort)
        {
            var id = ID.RandomID;

            ErrorResponse peerError = new ErrorResponse();
            PingRequest request = new PingRequest
            {
                RandomID = id.Value,
                Sender = sender.ID.Value
            };
            PingResponse response = null;

            var remoteEndPoint = new IPEndPoint(host, (Int32)tcpPort);
            GetClient(sender.ID, remoteEndPoint, out var client, out var isInPool, out var isConnected);
            Boolean timeout;
            if(isConnected)
            {
                try
                {
                    ClientStart(isInPool, client, remoteEndPoint, request, out response);
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
                Sender = sender.ID.Value,
                Key = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
                RandomID = id.Value,
                MessageOperation = MessageOperation.Store
            };

            var remoteContact = DiscoveryService.KnownContacts.SingleOrDefault(c => c.ID == key);
            GetClient(sender.ID, remoteContact.EndPoint, out var client, out var isInPool, out var isConnected);
            Boolean timeout;
            StoreResponse response = null;
            if (isConnected)
            {
                try
                {
                    ClientStart(isInPool, client, remoteContact.EndPoint, request, out response);
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

        private void GetClient(ID idOfClient, IPEndPoint remoteEndPoint, out SocketInConnetionPool client, out Boolean isInPool, out Boolean isConnected)
        {
            client = null;
            isInPool = false;
            isConnected = false;
            //connectionPool
            //connectionPool.TakeClient(idOfClient.Value, endPointOfClient, ConnectTimeout, out client, out isConnected, out isInPool);
            //if (client == null)
            //{
            //    client = new DiscoveryServiceSocket(endPointOfClient.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            //}
        }

        private void ClientStart<TResponse>(Boolean isInPool, SocketInConnetionPool client, IPEndPoint endPointOfClient, Request request, out TResponse response)
            where TResponse: Response
        {
            response = null;
            //Boolean isConnected = client.Connected;
            //if(!isInPool)
            //{
            //    connectionPool.PutSocketInPool(client, idOfClient.Value, endPointOfClient, ConnectTimeout, out _, out _, out isConnected);
            //}
            //response = null;

            //if (isConnected)
            //{

            //    client.Send(request.ToByteArray(), SendTimeout, out var isSent);

            //    if (isSent)
            //    {
            //        var bytesOfResponse = client.Receive(ReceiveTimeout, out var isReceived);
            //        if (isReceived)
            //        {
            //            response.Read(bytesOfResponse);
            //        }
            //    }
            //}
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, IPAddress host, Int32 tcpPort)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError = false;

            var request = new FindNodeRequest
            {
                Sender = sender.ID.Value,
                Key = keyToFindContacts.Value,
                RandomID = id.Value,
                MessageOperation = MessageOperation.FindNode
            };

            FindNodeResponse response = null;

            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContacts);
            GetClient(sender.ID, remoteContact.EndPoint, out var client, out var isInPool, out var isConnected);

            try
            {
                ClientStart(isInPool, client, remoteContact.EndPoint, request, out response);

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

            var request = new FindValueRequest
            {
                IdOfContact = keyToFindContact.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id.Value,
            };
            
            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContact);
            FindValueResponse response = null;
            GetClient(sender.ID, remoteContact.EndPoint, out var client, out var isInPool, out var isConnected);

            try
            {
                ClientStart(isInPool, client, remoteContact.EndPoint, request, out response);

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
