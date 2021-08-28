using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
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

namespace LUC.DiscoveryService.Messages.KademliaRequests
{
    public abstract class Request : Message
    {
        private static ConnectionPool connectionPool;
        private static ILoggingService log;

        public BigInteger RandomID { get; set; }
        public BigInteger Sender { get; set; }
        public UInt16 ProtocolVersion { get; set; } = 1;

        /// <summary>
        /// Returns whether received right response to <a href="last"/> request
        /// </summary>
        public Boolean IsReceivedLastRightResp { get; private set; } = false;

        static Request()
        {
            connectionPool = ConnectionPool.Instance();
            log = new LoggingService();
        }

        /// <inheritdoc/>
        public override IWireSerialiser Read(WireReader reader)
        {
            if (reader != null)
            {
                base.Read(reader);

                Sender = reader.ReadBigInteger();
                RandomID = reader.ReadBigInteger();

                return this;
            }
            else
            {
                throw new ArgumentNullException("ReaderNullException");
            }
        }

        /// <inheritdoc/>
        public override void Write(WireWriter writer)
        {
            if (writer != null)
            {
                base.Write(writer);

                writer.Write(Sender);
                writer.Write(RandomID);
            }
            else
            {
                throw new ArgumentNullException("WriterNullException");
            }
        }

        public void GetRequestResult<TResponse>(Contact remoteContact, out TResponse response, out RpcError rpcError)
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
                ClientStart(ipEndPoint, out isTimeoutSocketOp, out nodeError, out response);

                if (response != null)
                {
                    log.LogInfo($"The response is received:\n{response}");
                }
                //else
                //{
                //    remoteContact.TryRemoveIpAddress(cloneIpAddresses[numAddress], isRemoved: out _);
                //    cloneIpAddresses.RemoveAt(numAddress);
                //}
            }

            rpcError = RpcError(RandomID, response, isTimeoutSocketOp, nodeError);
            IsReceivedLastRightResp = !rpcError.HasError;

            if(!IsReceivedLastRightResp)
            {
                TryToEvictContact(remoteContact);
            }
        }

        private void ClientStart<TResponse>(EndPoint remoteEndPoint, out Boolean isTimeoutSocketOp, 
            out ErrorResponse nodeError, out TResponse response)
            where TResponse : Response, new()
        {
            nodeError = null;
            isTimeoutSocketOp = false;
            response = null;
            var bytesOfRequest = this.ToByteArray();

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
                log.LogInfo($"Request {GetType().Name} is sent to {client.Id}:\n" +
                            $"{this}\n");

                Int32 countCheck = 0;
                while ((client.Available == 0) && (countCheck <= Constants.MaxCheckAvailableData))
                {
                    Thread.Sleep(Constants.TimeCheckDataToRead);
                    countCheck++;
                }

                if (countCheck <= Constants.MaxCheckAvailableData)
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

        private RpcError RpcError(BigInteger id, Response resp, bool timeoutError, ErrorResponse peerError)
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

        private void TryToEvictContact(Contact remoteContact)
        {
            var dht = NetworkEventInvoker.DistributedHashTable(ProtocolVersion);
            var newContactInDht = dht.PendingContacts.FirstOrDefault();

            dht.DelayEviction(remoteContact, newContactInDht);
        }
    }
}
