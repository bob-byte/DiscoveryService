using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.DiscoveryServices.Kademlia.ClientPool;
using LUC.DiscoveryServices.Kademlia.Exceptions;
using LUC.DiscoveryServices.Messages;
using LUC.DiscoveryServices.Messages.KademliaRequests;
using LUC.DiscoveryServices.Messages.KademliaResponses;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Extensions;

using Nito.AsyncEx;

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

namespace LUC.DiscoveryServices.Kademlia
{
    internal class RemoteProcedureCaller
    {
        private const Int32 MAX_COUNT_TIMES_READ_MALFORMED_OR_OLD_MESS = 3;

        private static readonly TimeSpan s_timeWaitReadMalformedMess = TimeSpan.FromSeconds( value: 0.5 );

        private static readonly ConnectionPool s_connectionPool = ConnectionPool.Instance;

        //TODO: take away using TResponse and Request types
        public async ValueTask<(TResponse response, RpcError rpcError)> PostAsync<TResponse>(
            Request request,
            IPEndPoint remoteEndPoint,
            IoBehavior ioBehavior,
            CancellationToken cancellationToken)

            where TResponse: Response
        {
            TResponse response = null;
            RpcError rpcError;

            ConnectionPool.Socket client = null;

            try
            {
                //we can change network, so it is better to check IP-address
                //and don't wait result from unreachable IP-address
                Boolean canBeReachable = remoteEndPoint.Address.CanBeReachable();
                if ( !canBeReachable )
                {
                    rpcError = new RpcError
                    {
                        OtherError = true,
                        ErrorMessage = $"{remoteEndPoint} cannot be reachable"
                    };
                }
                else
                {
                    client = await s_connectionPool.SocketAsync( 
                        remoteEndPoint, 
                        DsConstants.ConnectTimeout,
                        ioBehavior, 
                        DsConstants.TimeWaitSocketReturnedToPool 
                    ).ConfigureAwait( continueOnCapturedContext: false );

                    if ( client.Available > 0 )
                    {
                        DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Found available bytes {client.Available} for read by {client.Id} socket before sending {request.GetType().Name}".WithAttention() );
                        await CleanExtraBytesAsync( client, ioBehavior ).ConfigureAwait( false );
                    }

                    Byte[] bytesOfRequest = request.ToByteArray();
                    client = await client.DsSendWithAvoidNetworkErrorsAsync( 
                        bytesOfRequest,
                        DsConstants.SendTimeout, 
                        DsConstants.ConnectTimeout, 
                        ioBehavior 
                    ).ConfigureAwait( false );
#if DEBUG
                    DsLoggerSet.DefaultLogger.LogInfo( $"Request {request.GetType().Name} is sent to {client.Id}:\n" +
                                $"{request}\n" );
#endif

                    Byte[] bytesOfResponse = ioBehavior == IoBehavior.Asynchronous ?
                        await client.DsReceiveAsync( DsConstants.ReceiveTimeout, cancellationToken ).ConfigureAwait( false ) :
                        client.DsReceive( DsConstants.ReceiveTimeout, cancellationToken );

                    if ( bytesOfResponse[ 0 ] != (Byte)MessageOperation.LocalError )
                    {
                        response = (TResponse)Activator.CreateInstance(
                            typeof( TResponse ),
                            bytesOfResponse
                        );

                        rpcError = new RpcError
                        {
                            IDMismatchError = request.RandomID != response.RandomID
                        };

#if DEBUG
                        DsLoggerSet.DefaultLogger.LogInfo( $"The response is received ({bytesOfResponse.Length} bytes):\n{response}" );
#endif
                    }
                    else
                    {
                        var nodeErrorResp = new ErrorResponse( bytesOfResponse );
                        rpcError = new RpcError
                        {
                            IDMismatchError = request.RandomID != nodeErrorResp.RandomID,
                            ErrorMessage = nodeErrorResp.ErrorMessage,
                            RemoteError = true
                        };
                    }
                }
            }
            catch ( TimeoutException ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                HandleCatchedException( ex, out rpcError );
            }
            catch ( SocketException ex )
            {
                HandleCatchedException( ex, out rpcError );
            }
            //during read close contacts from stream, read contact which has incorrect TCP port (see method AbstractDsData.CheckTcpPort)
            catch ( ArgumentException ex )
            {
                HandleCatchedException( ex, out rpcError );
            }
            catch ( MalfactorAttackException ex )
            {
                HandleCatchedException( ex, out rpcError );
            }
            catch ( EndOfStreamException ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                HandleCatchedException( ex, out rpcError );
            }
            //if it have been tried to read bytes from connection, but it was read 0 bytes
            catch ( IndexOutOfRangeException ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                HandleCatchedException( ex, out rpcError );
            }
            catch ( AggregateException ex )
            {
                DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                HandleCatchedException( ex, out rpcError );
            }
            catch ( InvalidOperationException ex )
            {
                HandleCatchedException( ex, out rpcError );
            }
            finally
            {
                if ( client != null )
                {
                    await client.ReturnToPoolAsync( 
                        DsConstants.ConnectTimeout, 
                        ioBehavior 
                    ).ConfigureAwait( false );
                }
            }

            if ( rpcError.HasError )
            {
                DsLoggerSet.DefaultLogger.LogInfo( rpcError.ToString() );
            }

            return (response, rpcError);
        }

        private async ValueTask CleanExtraBytesAsync( ConnectionPool.Socket client, IoBehavior ioBehavior )
        {
            Int32 countReadBytes = 0;

            while ( ( client.Available > 0 ) && ( countReadBytes < DsConstants.MAX_AVAILABLE_READ_BYTES ) )
            {
                //use always async method, because ReceiveTimeout works only for async methods
                Task<Int32> taskReceive = client.ReceiveAsync( 
                    buffer: new ArraySegment<Byte>( new Byte[ client.Available ] ), 
                    SocketFlags.None 
                );
                if ( ioBehavior == IoBehavior.Asynchronous )
                {
                    countReadBytes += await taskReceive.ConfigureAwait( continueOnCapturedContext: false );
                }
                else if ( ioBehavior == IoBehavior.Synchronous )
                {
                    countReadBytes += AsyncContext.Run( () => taskReceive );
                }

                await WaitAsync( ioBehavior, s_timeWaitReadMalformedMess ).ConfigureAwait( false );
            }

            if ( ( countReadBytes == MAX_COUNT_TIMES_READ_MALFORMED_OR_OLD_MESS ) && ( client.Available > 0 ) )
            {
                String message = $"Malfactor attacks communication with {client.Id}.";
                throw new MalfactorAttackException( message );
            }
        }

        private async ValueTask WaitAsync( IoBehavior ioBehavior, TimeSpan timeToWait )
        {
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                await Task.Delay( timeToWait ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else
            {
                Thread.Sleep( timeToWait );
            }
        }

        private void HandleCatchedException( Exception exception, out RpcError rpcError ) => 
            rpcError = new RpcError
            {
                LocalError = !(exception is TimeoutException),
                RemoteError = exception is TimeoutException,
                ErrorMessage = exception.Message,
            };
    }
}
