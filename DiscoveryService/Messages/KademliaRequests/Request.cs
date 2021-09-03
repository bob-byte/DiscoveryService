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
    abstract class Request : Message
    {
        private static ConnectionPool connectionPool;
        private static ILoggingService log;

        static Request()
        {
            connectionPool = ConnectionPool.Instance();
            log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        public Request()
        {
            RandomID = ID.RandomID.Value;
        }

        public BigInteger RandomID { get; private set; }
        public BigInteger Sender { get; set; }

        /// <summary>
        /// Returns whether received right response to <a href="last"/> request
        /// </summary>
        public Boolean IsReceivedLastRightResp { get; private set; } = false;

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

        public void GetResult<TResponse>(Contact remoteContact, UInt16 protocolVersion, out TResponse response, out RpcError rpcError)
            where TResponse : Response, new()
        {
            (response, rpcError) = ResultAsync<TResponse>(remoteContact, IOBehavior.Synchronous, protocolVersion).GetAwaiter().GetResult();
        }

        public async Task<(TResponse, RpcError)> ResultAsync<TResponse>(Contact remoteContact, UInt16 protocolVersion)
            where TResponse : Response, new()
        {
            return await ResultAsync<TResponse>(remoteContact, IOBehavior.Asynchronous, protocolVersion).ConfigureAwait(continueOnCapturedContext: false);
        }

        public async Task<(TResponse, RpcError)> ResultAsync<TResponse>(Contact remoteContact, IOBehavior ioBehavior, UInt16 protocolVersion)
            where TResponse : Response, new()
        {
            ErrorResponse nodeError = null;
            Boolean isTimeoutSocketOp = false;
            TResponse response = null;

            var cloneIpAddresses = remoteContact.IpAddresses();
            for (Int32 numAddress = cloneIpAddresses.Count - 1;
                (numAddress >= 0) && (response == null); numAddress--)
            {
                var ipEndPoint = new IPEndPoint(cloneIpAddresses[numAddress], remoteContact.TcpPort);
                (isTimeoutSocketOp, nodeError, response) = await ClientStartAsync<TResponse>(ipEndPoint, ioBehavior).ConfigureAwait(continueOnCapturedContext: false);

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

            RpcError rpcError = RpcError(RandomID, response, isTimeoutSocketOp, nodeError);
            IsReceivedLastRightResp = !rpcError.HasError;

            if (!IsReceivedLastRightResp)
            {
                TryToEvictContact(remoteContact, protocolVersion);
            }

            return (response, rpcError);
        }

        private async Task<(Boolean, ErrorResponse, TResponse)> ClientStartAsync<TResponse>(EndPoint remoteEndPoint, IOBehavior ioBehavior)
            where TResponse : Response, new()
        {
            ErrorResponse nodeError = null;
            Boolean isTimeoutSocketOp = false;
            TResponse response = null;

            ConnectionPoolSocket client = null;
            try
            {
                Byte[] bytesOfRequest = this.ToByteArray();

                client = await connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout,
                    ioBehavior, Constants.TimeWaitReturnToPool).ConfigureAwait(continueOnCapturedContext: false);

                //clean extra bytes
                if (client.Available > 0)
                {
                    await client.ReceiveAsync(ioBehavior, Constants.ReceiveTimeout).ConfigureAwait(false);
                }

                client = await client.SendWithAvoidErrorsInNetworkAsync(bytesOfRequest,
                    Constants.SendTimeout, Constants.ConnectTimeout, ioBehavior).ConfigureAwait(false);
                log.LogInfo($"Request {GetType().Name} is sent to {client.Id}:\n" +
                            $"{this}\n");

                Int32 countCheck = 0;
                while ((client.Available == 0) && (countCheck <= Constants.MaxCheckAvailableData))
                {
                    await Wait(ioBehavior, Constants.TimeCheckDataToRead).ConfigureAwait(false);

                    countCheck++;
                }

                if (countCheck <= Constants.MaxCheckAvailableData)
                {
                    Byte[] bytesOfResponse = await client.ReceiveAsync(ioBehavior, Constants.ReceiveTimeout).ConfigureAwait(false);

                    response = new TResponse();
                    response.Read(bytesOfResponse);
                }
                else
                {
                    await client.DisconnectAsync(ioBehavior, reuseSocket: false).ConfigureAwait(false);
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
            catch (ArgumentException ex)
            {
                isTimeoutSocketOp = false;
                HandleException(ex, ref nodeError);
            }
            finally
            {
                client?.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }

            return (isTimeoutSocketOp, nodeError, response);
        }

        private async Task Wait(IOBehavior ioBehavior, TimeSpan timeToWait)
        {
            if (ioBehavior == IOBehavior.Asynchronous)
            {
                await Task.Delay(timeToWait);
            }
            else
            {
                Thread.Sleep(timeToWait);
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

        private void TryToEvictContact(Contact remoteContact, UInt16 protocolVersion)
        {
            var dht = NetworkEventInvoker.DistributedHashTable(protocolVersion);

            //"toReplace: null", because we don't get new contact in Kademlia request
            dht.DelayEviction(remoteContact, toReplace: null);
        }
    }
}
