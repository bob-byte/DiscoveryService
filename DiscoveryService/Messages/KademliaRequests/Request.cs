using LUC.DiscoveryService.CodingData;
using LUC.DiscoveryService.Common;
using LUC.DiscoveryService.Interfaces;
using LUC.DiscoveryService.Kademlia;
using LUC.DiscoveryService.Kademlia.ClientPool;
using LUC.DiscoveryService.Messages.KademliaResponses;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private static readonly ConnectionPool s_connectionPool;
        private static readonly ILoggingService s_log;

        static Request()
        {
            s_connectionPool = ConnectionPool.Instance();
            s_log = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        public Request( BigInteger sender )
            : this()
        {
            Sender = sender;
        }

        public Request()
        {
            //We don't use DefaultInit, because it can be overridden in child classes
            RandomID = KademliaId.RandomID.Value;
            IsReceivedLastRightResp = false;
        }

        public BigInteger RandomID { get; private set; }
        public BigInteger Sender { get; private set; }

        /// <summary>
        /// Returns whether received right response to <a href="last"/> request
        /// </summary>
        //It is internal to not show all bytes in log(see method Display.ObjectToString)
        internal Boolean IsReceivedLastRightResp { get; private set; }

        /// <inheritdoc/>
        public override IWireSerialiser Read( WireReader reader )
        {
            if ( reader != null )
            {
                base.Read( reader );

                Sender = reader.ReadBigInteger();
                RandomID = reader.ReadBigInteger();

                return this;
            }
            else
            {
                throw new ArgumentNullException( "ReaderNullException" );
            }
        }

        /// <inheritdoc/>
        public override void Write( WireWriter writer )
        {
            if ( writer != null )
            {
                base.Write( writer );

                writer.Write( Sender );
                writer.Write( RandomID );
            }
            else
            {
                throw new ArgumentNullException( "WriterNullException" );
            }
        }

        public void GetResult<TResponse>( Contact remoteContact, UInt16 protocolVersion, out TResponse response, out RpcError rpcError )
            where TResponse : Response => (response, rpcError) = ResultAsync<TResponse>( remoteContact, IOBehavior.Synchronous, protocolVersion ).GetAwaiter().GetResult();

        public async Task<(TResponse, RpcError)> ResultAsync<TResponse>( Contact remoteContact, UInt16 protocolVersion )
            where TResponse : Response => await ResultAsync<TResponse>( remoteContact, IOBehavior.Asynchronous, protocolVersion ).ConfigureAwait( continueOnCapturedContext: false );

        public async Task<(TResponse, RpcError)> ResultAsync<TResponse>( Contact remoteContact, IOBehavior ioBehavior, UInt16 protocolVersion )
            where TResponse : Response
        {
            ErrorResponse nodeError = null;
            Boolean isTimeoutSocketOp = false;
            TResponse response = null;

            List<IPAddress> cloneIpAddresses = remoteContact.IpAddresses();
            for ( Int32 numAddress = cloneIpAddresses.Count - 1;
                ( numAddress >= 0 ) && ( response == null ); numAddress-- )
            {
                IPEndPoint ipEndPoint = new IPEndPoint( cloneIpAddresses[ numAddress ], remoteContact.TcpPort );
                (isTimeoutSocketOp, nodeError, response) = await ClientStartAsync<TResponse>( ipEndPoint, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );

                if ( response != null )
                {
                    remoteContact.LastActiveIpAddress = cloneIpAddresses[ numAddress ];

                    ResetEvictionCount( remoteContact, protocolVersion );
                }
            }

            RpcError rpcError = RpcError( RandomID, response, isTimeoutSocketOp, nodeError );
            IsReceivedLastRightResp = !rpcError.HasError;

            if ( !IsReceivedLastRightResp )
            {
                TryToEvictContact( remoteContact, protocolVersion );
            }

            return (response, rpcError);
        }

        private async Task<(Boolean isTimeoutSocketOp, ErrorResponse nodeError, TResponse response)> ClientStartAsync<TResponse>( IPEndPoint remoteEndPoint, IOBehavior ioBehavior )
            where TResponse : Response
        {
            ErrorResponse nodeError = null;
            Boolean isTimeoutSocketOp = false;
            TResponse response = null;

            ConnectionPoolSocket client = null;

            try
            {
                Boolean isTheSameNetwork = IpAddressFilter.IsIpAddressInTheSameNetwork( remoteEndPoint.Address );
                if ( isTheSameNetwork )
                {

                    Byte[] bytesOfRequest = ToByteArray();

                    client = await s_connectionPool.SocketAsync( remoteEndPoint, Constants.ConnectTimeout,
                        ioBehavior, Constants.TimeWaitReturnToPool ).ConfigureAwait( continueOnCapturedContext: false );

                    //clean extra bytes
                    if ( client.Available > 0 )
                    {
                        await client.DsReceiveAsync( ioBehavior, Constants.ReceiveTimeout ).ConfigureAwait( false );
                    }

                    client = await client.DsSendWithAvoidErrorsInNetworkAsync( bytesOfRequest,
                        Constants.SendTimeout, Constants.ConnectTimeout, ioBehavior ).ConfigureAwait( false );
                    s_log.LogInfo( $"Request {GetType().Name} is sent to {client.Id}:\n" +
                                $"{this}\n" );

                    Int32 countCheck = 0;
                    while ( ( client.Available == 0 ) && ( countCheck <= Constants.MAX_CHECK_AVAILABLE_DATA ) )
                    {
                        await Wait( ioBehavior, Constants.TimeCheckDataToRead ).ConfigureAwait( false );

                        countCheck++;
                    }

                    if ( countCheck <= Constants.MAX_CHECK_AVAILABLE_DATA )
                    {
                        Byte[] bytesOfResponse = await client.DsReceiveAsync( ioBehavior, Constants.ReceiveTimeout ).ConfigureAwait( false );

                        response = (TResponse)Activator.CreateInstance( typeof( TResponse ), RandomID );
                        response.Read( bytesOfResponse );

                        s_log.LogInfo( $"The response is received ({bytesOfResponse.Length} bytes):\n{response}" );
                    }
                    else
                    {
                        await client.DsDisconnectAsync( ioBehavior, reuseSocket: false, Constants.DisconnectTimeout ).ConfigureAwait( false );
                        throw new TimeoutException();
                    }
                }
            }
            catch ( TimeoutException ex )
            {
                isTimeoutSocketOp = true;
                HandleException( ex, ref nodeError );
            }
            catch ( SocketException ex )
            {
                isTimeoutSocketOp = false;
                HandleException( ex, ref nodeError );
            }
            catch ( EndOfStreamException ex )
            {
                isTimeoutSocketOp = false;
                HandleException( ex, ref nodeError );
            }
            catch ( ArgumentException ex )
            {
                isTimeoutSocketOp = false;
                HandleException( ex, ref nodeError );
            }
            catch ( Win32Exception ex )
            {
                isTimeoutSocketOp = false;
                HandleException( ex, ref nodeError );
            }
            finally
            {
                client?.ReturnedToPool();
            }

            return (isTimeoutSocketOp, nodeError, response);
        }



        private async Task Wait( IOBehavior ioBehavior, TimeSpan timeToWait )
        {
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await Task.Delay( timeToWait );
            }
            else
            {
                Thread.Sleep( timeToWait );
            }
        }

        private void HandleException( Exception exception, ref ErrorResponse nodeError )
        {
            nodeError = new ErrorResponse( RandomID )
            {
                ErrorMessage = exception.Message
            };
            s_log.LogError( exception.ToString() );
        }

        private RpcError RpcError( BigInteger id, Response resp, Boolean timeoutError, ErrorResponse peerError )
        {
            RpcError rpcError = new RpcError
            {
                TimeoutError = timeoutError,
                PeerError = peerError != null,
                PeerErrorMessage = peerError?.ErrorMessage
            };

            if ( ( resp != null ) && ( id != default ) )
            {
                rpcError.IDMismatchError = id != resp.RandomID;
            }
            else
            {
                rpcError.IDMismatchError = false;
            }

            s_log.LogInfo( rpcError.ToString() );
            return rpcError;
        }

        private void ResetEvictionCount( Contact remoteContact, UInt16 protocolVersion )
        {
            Dht dht = NetworkEventInvoker.DistributedHashTable( protocolVersion );
            dht.EvictionCount[ remoteContact.KadId.Value ] = 0;
        }

        private void TryToEvictContact( Contact remoteContact, UInt16 protocolVersion )
        {
            Dht dht = NetworkEventInvoker.DistributedHashTable( protocolVersion );

            //"toReplace: null", because we don't get new contact in Kademlia request
            dht.DelayEviction( remoteContact, toReplace: null );
        }
    }
}
