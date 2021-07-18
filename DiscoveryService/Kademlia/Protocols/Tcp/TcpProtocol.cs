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

            ErrorResponse peerError = null;
            PingRequest request = new PingRequest((UInt32)sender.EndPoint.Port)
            {
                RandomID = id.Value,
                Sender = sender.ID.Value,
                MessageOperation = MessageOperation.Ping
            };
            PingResponse response = null;

            var remoteEndPoint = new IPEndPoint(host, (Int32)tcpPort);
            Boolean timeout;
            try
            {
                ClientStart(remoteEndPoint, request, out response);
                timeout = false;
            }
            catch (TimeoutException ex)
            {
                timeout = true;
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                timeout = false;
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
            }

            return RpcError(id, response, timeout, peerError);
        }

        private void ClientStart<TResponse>(IPEndPoint remoteEndPoint, Request request, out TResponse response)
            where TResponse : Response, new()
        {
            response = null;
            var bytesOfRequest = request.ToByteArray();

            var client = connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout, IOBehavior.Synchronous, Constants.TimeWaitReturnToPool).GetAwaiter().GetResult();

            try
            {
                Boolean isReceived = false;

                client.Send(bytesOfRequest, Constants.SendTimeout, out var isSent);
                if (!isSent)
                {
                    client.Connect(remoteEndPoint, Constants.ConnectTimeout, out var isConnected);
                    if (isConnected)
                    {
                        client.Send(bytesOfRequest, Constants.SendTimeout, out isSent);
                    }
                }

                if (isSent)
                {
                    Thread.Sleep(Constants.TimeWaitResponse);
                    var bytesOfResponse = client.Receive(Constants.ReceiveTimeout, out isReceived);
                    if (isReceived)
                    {
                        response = new TResponse();
                        response.Read(bytesOfResponse);
                    }
                }

                if(!isSent || !isReceived)
                {
                    throw new TimeoutException();
                }
            }
            finally
            {
                client.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            ID id = ID.RandomID;
            var request = new StoreRequest((UInt32)sender.EndPoint.Port)
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

            ErrorResponse peerError = null;
            StoreResponse response = null;
            Boolean timeout;

            try
            {
                ClientStart(remoteContact.EndPoint, request, out response);
                timeout = false;
            }
            catch (TimeoutException ex)
            {
                timeout = true;
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                timeout = false;
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
            }

            return RpcError(id, response, timeout, peerError);
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, IPAddress host, Int32 tcpPort)
        {
            ID id = ID.RandomID;
            Boolean timeoutError = false;
            var request = new FindNodeRequest((UInt32)sender.EndPoint.Port)
            {
                Sender = sender.ID.Value,
                Key = keyToFindContacts.Value,
                RandomID = id.Value,
                MessageOperation = MessageOperation.FindNode
            };
            FindNodeResponse response = null;
            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContacts);
            ErrorResponse peerError = null;
            List<Contact> closeContactsToKey = null;

            try
            {
                ClientStart(remoteContact.EndPoint, request, out response);

                //get close contacts near key
                closeContactsToKey = response.Contacts;
            }
            catch (SocketException ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = true;
            }

            return (closeContactsToKey ?? EmptyContactList(), RpcError(id, response, timeoutError, peerError));
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID keyToFindContact)
        {
            ID id = ID.RandomID;
            bool timeoutError = false;

            var request = new FindValueRequest((UInt32)sender.EndPoint.Port)
            {
                IdOfContact = keyToFindContact.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id.Value,
            };
            
            var remoteContact = DiscoveryService.KnownContacts.Single(c => c.ID == keyToFindContact);
            FindValueResponse response = null;
            List<Contact> closeContactsToKey = null;
            ErrorResponse peerError = new ErrorResponse();

            try
            {
                ClientStart(remoteContact.EndPoint, request, out response);

                //get close contacts near key
                closeContactsToKey = response?.CloseContactsToRepsonsingPeer/*.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList()*/;

                // Return only contacts with supported protocols.
                //return (contacts?.Where(c => c.Protocol != null).ToList(), ret.Value, GetRpcError(id, ret, timeoutError, error));
            }
            catch(TimeoutException ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = true;
            }
            catch (Exception ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = false;
            }

            return (closeContactsToKey ?? EmptyContactList(), response?.ValueInResponsingPeer, RpcError(id, response, timeoutError, peerError));
        }

        protected RpcError RpcError(ID id, Response resp, bool timeoutError, ErrorResponse peerError)
        {
            RpcError rpcError = new RpcError
            {
                TimeoutError = timeoutError,
                PeerError = peerError != null,
                PeerErrorMessage = peerError?.ErrorMessage
            };

            if((resp != null) && (id != new ID(bi: default)))
            {
                rpcError.IDMismatchError = id != resp.RandomID;
            }
            else
            {
                rpcError.IDMismatchError = false;
            }

            return rpcError;
        }
    }
}
