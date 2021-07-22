using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages;
using LUC.DiscoveryService.Messages.KademliaRequests;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia
{
    class KademliaOperation
    {
        private static ILoggingService log;
        private readonly UInt32 protocolVersion;
        private static ConnectionPool connectionPool;

        public KademliaOperation()
        {
            ;//do nothing
        }

        public KademliaOperation(ILoggingService loggingService, UInt32 protocolVersion)
        {
            this.protocolVersion = protocolVersion;

            log = loggingService;
            connectionPool = ConnectionPool.Instance(loggingService);
        }

        /// <inheritdoc/>
        public RpcError Ping(Contact sender, EndPoint endPointToPing)
        {
            var id = ID.RandomID;

            ErrorResponse peerError = null;
            PingRequest request = new PingRequest
            {
                RandomID = id.Value,
                Sender = sender.ID.Value,
                MessageOperation = MessageOperation.Ping
            };
            PingResponse response = null;

            Boolean timeout;
            try
            {
                ClientStart(endPointToPing, request, out response);
                timeout = false;

                log.LogInfo($"The response is received:\n{response}");
            }
            catch (TimeoutException ex)
            {
                timeout = true;

                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());
            }
            catch (Exception ex)
            {
                timeout = false;

                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());
            }

            return RpcError(id, response, timeout, peerError);
        }

        private void ClientStart<TResponse>(EndPoint remoteEndPoint, Request request, out TResponse response)
            where TResponse : Response, new()
        {
            response = null;
            var bytesOfRequest = request.ToByteArray();

            var client = connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout, IOBehavior.Synchronous, Constants.TimeWaitReturnToPool).GetAwaiter().GetResult();

            try
            {
                Boolean isReceived = false;

                SendWithAvoidErrorsInNetwork(bytesOfRequest, Constants.SendTimeout, Constants.ConnectTimeout,
                    ref client, out var isSent);
                if (isSent)
                {
                    //TODO: optimize it. Check client.Available every 0.4 s
                    Thread.Sleep(Constants.TimeWaitResponse);

                    var bytesOfResponse = client.Receive(Constants.ReceiveTimeout, out isReceived);
                    if (isReceived)
                    {
                        response = new TResponse();
                        response.Read(bytesOfResponse);
                    }
                }

                if (!isSent || !isReceived)
                {
                    throw new TimeoutException();
                }
            }
            finally
            {
                client.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        public static void SendWithAvoidErrorsInNetwork(Byte[] bytesToSend, TimeSpan timeoutToSend, TimeSpan timeoutToConnect,
            ref ConnectionPoolSocket client, out Boolean isSent)
        {
            client.Send(bytesToSend, timeoutToSend, out isSent);

            if (!isSent)
            {
                client = new ConnectionPoolSocket(client.Id.AddressFamily, SocketType.Stream, ProtocolType.Tcp, client.Id, client.Pool, client.Log);
                client.Connect(client.Id, timeoutToConnect, out var isConnected);

                if (isConnected)
                {
                    client.Send(bytesToSend, timeoutToSend, out isSent);
                }
            }
        }

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, EndPoint remoteEndPoint, bool isCached = false, int expirationTimeSec = 0)
        {
            ID id = ID.RandomID;
            var request = new StoreRequest
            {
                Sender = sender.ID.Value,
                KeyToStore = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
                RandomID = id.Value,
                MessageOperation = MessageOperation.Store
            };

            var remoteContact = RemoteContact(key);

            ErrorResponse peerError = null;
            StoreResponse response = null;
            Boolean timeout;

            try
            {
                ClientStart(remoteEndPoint, request, out response);
                timeout = false;

                log.LogInfo($"The response is received:\n{response}");
            }
            catch (TimeoutException ex)
            {
                timeout = true;

                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());
            }
            catch (Exception ex)
            {
                timeout = false;

                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());
            }

            return RpcError(id, response, timeout, peerError);
        }

        private Contact RemoteContact(ID key)
        {
            Validate.IsTrue<ArgumentNullException>(key != new ID(default(BigInteger)), $"{nameof(key)} is null");

            return DiscoveryService.KnownContacts(protocolVersion).SingleOrDefault(c => c.Key == key.Value).Value;
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, EndPoint remoteEndPoint)
        {
            ID id = ID.RandomID;
            Boolean timeoutError = false;
            var request = new FindNodeRequest
            {
                Sender = sender.ID.Value,
                ContactId = keyToFindContacts.Value,
                RandomID = id.Value,
                MessageOperation = MessageOperation.FindNode
            };
            FindNodeResponse response = null;
            var remoteContact = RemoteContact(keyToFindContacts);
            ErrorResponse peerError = null;
            List<Contact> closeContactsToKey = null;

            try
            {
                ClientStart(remoteEndPoint, request, out response);

                //get close contacts near key
                closeContactsToKey = response.CloseSenderContacts.ToList();

                log.LogInfo($"The response is received:\n{response}");
            }
            catch (SocketException ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());

                timeoutError = true;
            }

            return (closeContactsToKey ?? EmptyContactList(), RpcError(id, response, timeoutError, peerError));
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID keyToFindContact, EndPoint remoteEndPoint)
        {
            ID id = ID.RandomID;
            bool timeoutError = false;

            var request = new FindValueRequest
            {
                ContactId = keyToFindContact.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id.Value,
            };

            var remoteContact = RemoteContact(keyToFindContact);
            FindValueResponse response = null;
            List<Contact> closeContactsToKey = null;
            ErrorResponse peerError = new ErrorResponse();

            try
            {
                ClientStart(remoteEndPoint, request, out response);

                //get close contacts near key
                closeContactsToKey = response?.CloseContactsToRepsonsingPeer.ToList()/*.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList()*/;

                log.LogInfo($"The response is received:\n{response}");
                // Return only contacts with supported protocols.
                //return (contacts?.Where(c => c.Protocol != null).ToList(), ret.Value, GetRpcError(id, ret, timeoutError, error));
            }
            catch (TimeoutException ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());

                timeoutError = true;
            }
            catch (Exception ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                log.LogError(ex.ToString());

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

            if ((resp != null) && (id != new ID(bi: default)))
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
