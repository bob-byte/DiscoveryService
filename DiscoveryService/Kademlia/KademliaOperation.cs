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

        private void GetRequestResult<TResponse>(Contact remoteContact, Request request, 
            out TResponse response, out RpcError rpcError) 
            where TResponse : Response, new()
        {
            ErrorResponse nodeError = null;
            Boolean isTimeoutSocketOp = false;
            response = null;

            var cloneIpAddresses = remoteContact.IpAddresses();
            for (Int32 numAddress = cloneIpAddresses.Count - 1;
                (numAddress >= 0) && (response == null); numAddress--)
            {
                var ipEndPoint = new IPEndPoint(cloneIpAddresses[numAddress], remoteContact.TcpPort);
                ClientStart(ipEndPoint, request, out isTimeoutSocketOp, out nodeError, out response);

                if (response != null)
                {
                    log.LogInfo($"The response is received:\n{response}");
                }
                else
                {
                    remoteContact.TryRemoveIpAddress(cloneIpAddresses[numAddress], isRemoved: out _);
                    cloneIpAddresses.RemoveAt(numAddress);
                }
            }            

            rpcError = RpcError(request.RandomID, response, isTimeoutSocketOp, nodeError);
        }

        private void ClientStart<TResponse>(EndPoint remoteEndPoint, Request request, 
            out Boolean isTimeoutSocketOp, out ErrorResponse nodeError, out TResponse response)
            where TResponse : Response, new()
        {
            nodeError = null;
            isTimeoutSocketOp = false;
            response = null;
            var bytesOfRequest = request.ToByteArray();

            ConnectionPoolSocket client = null;
            try
            {
                client = connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout,
                IOBehavior.Synchronous, Constants.TimeWaitReturnToPool).Result;

                //clean extra bytes
                if (client.Available > 0)
                {
                    client.Receive(Constants.ReceiveTimeout);
                }

                ConnectionPoolSocket.SendWithAvoidErrorsInNetwork(bytesOfRequest, 
                    Constants.SendTimeout, Constants.ConnectTimeout, ref client);
                log.LogInfo($"Request {request.GetType().Name} is sent to {client.Id}:\n" +
                            $"{request}\n");

                Int32 countCheck = 0;
                while((client.Available == 0) && (countCheck <= Constants.MaxCheckAvailableData))
                {
                    Thread.Sleep(Constants.TimeCheckDataToRead);
                    countCheck++;
                }

                if(countCheck <= Constants.MaxCheckAvailableData)
                {
                    var bytesOfResponse = client.Receive(Constants.ReceiveTimeout);

                    response = new TResponse();
                    response.Read(bytesOfResponse);
                }
                else
                {
                    client.Disconnect(reuseSocket: false, Constants.DisconnectTimeout);
                    throw new TimeoutException();
                }
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
            finally
            {
                client?.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
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
                KeyToFindCloseContacts = keyToFindContacts.Value,
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
                KeyToFindCloseContacts = keyToFindContact.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id,
            };

            GetRequestResult<FindValueResponse>(remoteContact, request, out var response, out var rpcError);
            var closeContacts = response?.CloseContactsToRepsonsingPeer?.ToList() ?? EmptyContactList();

            return (closeContacts, response?.ValueInResponsingPeer, rpcError);
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
                rpcError.IDMismatchError = false;
            }

            log.LogInfo(rpcError.ToString());
            return rpcError;
        }
    }
}
