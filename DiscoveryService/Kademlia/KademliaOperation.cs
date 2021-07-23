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
    class KademliaOperation
    {
        private static ILoggingService log;
        private readonly UInt32 protocolVersion;
        private static ConnectionPool connectionPool;

        public KademliaOperation()
        {
            ;//do nothing
        }

        public KademliaOperation(UInt32 protocolVersion)
        {
            this.protocolVersion = protocolVersion;

            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
            connectionPool = ConnectionPool.Instance();
        }

        /// <inheritdoc/>
        public RpcError Ping(Contact sender, Contact remoteContact)
        {
            var id = ID.RandomID;
            PingRequest request = new PingRequest
            {
                RandomID = id.Value,
                Sender = sender.ID.Value,
                MessageOperation = MessageOperation.Ping
            };

            GetRequestResult<PingResponse>(remoteContact, request, response: out _, out var rpcError);
            return rpcError;
        }

        private void GetRequestResult<TResponse>(Contact contactToPing, Request request, out TResponse response, out RpcError rpcError) 
            where TResponse : Response, new()
        {
            ErrorResponse nodeError = null;
            Boolean isTimeoutSocketOp;
            response = null;

            try
            {
                var cloneIpAddresses = contactToPing.IpAddresses();
                for (Int32 numAddress = cloneIpAddresses.Count - 1;
                    (numAddress >= 0) && (response == null); numAddress--)
                {
                    var ipEndPoint = new IPEndPoint(cloneIpAddresses[numAddress], contactToPing.TcpPort);
                    ClientStart(ipEndPoint, request, out response);

                    if (response == null)
                    {
                        contactToPing.TryRemoveIpAddress(cloneIpAddresses[numAddress], out _);
                    }
                }

                isTimeoutSocketOp = false;
                log.LogInfo($"The response is received:\n{response}");
            }
            catch (TimeoutException ex)
            {
                isTimeoutSocketOp = true;
                HandleException(ex, ref nodeError);
            }
            catch (SocketException ex)
            {
                isTimeoutSocketOp = false;
                HandleException(ex, ref nodeError);
            }
            catch (EndOfStreamException ex)
            {
                isTimeoutSocketOp = false;
                HandleException(ex, ref nodeError);
            }

            rpcError = RpcError(request.RandomID, response, isTimeoutSocketOp, nodeError);
        }

        private void ClientStart<TResponse>(EndPoint remoteEndPoint, Request request, out TResponse response)
            where TResponse : Response, new()
        {
            response = null;
            var bytesOfRequest = request.ToByteArray();

            var client = connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout, IOBehavior.Synchronous, Constants.TimeWaitReturnToPool).GetAwaiter().GetResult();

            try
            {
                ConnectionPoolSocket.SendWithAvoidErrorsInNetwork(bytesOfRequest, 
                    Constants.SendTimeout, Constants.ConnectTimeout, ref client);

                var bytesOfResponse = client.Receive(Constants.ReceiveTimeout);

                response = new TResponse();
                response.Read(bytesOfResponse);
            }
            finally
            {
                client.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        private void HandleException(Exception exception, ref ErrorResponse nodeError)
        {
            nodeError = new ErrorResponse
            {
                ErrorMessage = exception.Message
            };
            log.LogError(exception.ToString());
        }

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, Contact remoteContact, bool isCached = false, int expirationTimeSec = 0)
        {
            var id = ID.RandomID.Value;
            var request = new StoreRequest
            {
                Sender = sender.ID.Value,
                KeyToStore = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
                RandomID = id,
                MessageOperation = MessageOperation.Store
            };

            //var remoteContact = RemoteContact(key);
            GetRequestResult<StoreResponse>(remoteContact, request, response: out _, out var rpcError);
            return rpcError;
        }

        private Contact RemoteContact(ID key)
        {
            Validate.IsTrue<ArgumentNullException>(key != new ID(default(BigInteger)), $"{nameof(key)} is null");

            return DiscoveryService.KnownContacts(protocolVersion).SingleOrDefault(c => c.Key == key.Value).Value;
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts, Contact remoteContact)
        {
            var id = ID.RandomID.Value;
            var request = new FindNodeRequest
            {
                Sender = sender.ID.Value,
                ContactId = keyToFindContacts.Value,
                RandomID = id,
                MessageOperation = MessageOperation.FindNode
            };
            GetRequestResult<FindNodeResponse>(remoteContact, request, out var response, out var rpcError);

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
                ContactId = keyToFindContact.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id,
            };

            GetRequestResult<FindValueResponse>(remoteContact, request, out var response, out var rpcError);
            var closeContacts = response?.CloseContactsToRepsonsingPeer?.ToList() ?? EmptyContactList();

            return (closeContacts, response.ValueInResponsingPeer, rpcError);
        }

        protected RpcError RpcError(BigInteger id, Response resp, bool timeoutError, ErrorResponse peerError)
        {
            RpcError rpcError = new RpcError
            {
                TimeoutError = timeoutError,
                PeerError = peerError != null,
                PeerErrorMessage = peerError?.ErrorMessage
            };

            if ((resp != null) && (id != default))
            {
                rpcError.IDMismatchError = id != resp.RandomID;
            }
            else
            {
                rpcError.IDMismatchError = true;
            }

            return rpcError;
        }
    }
}
