using DiscoveryServices.Common;
using DiscoveryServices.Common.Extensions;
using LUC.Interfaces.Extensions;

using Nito.AsyncEx;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices.Kademlia.ClientPool
{
    /// <summary>
    /// Client socket, maintained by the Connection Pool
    /// </summary>
    partial class ConnectionPool
    {
        internal partial class Socket : AsyncSocket
        {
            private readonly AsyncLock m_lockWaitingToTakeFromPool;
            private readonly AsyncAutoResetEvent m_canBeTakenFromPool;

            private readonly StateInPoolReference m_statusInPool;

            /// <inheritdoc/>
            public Socket( EndPoint remoteEndPoint, ConnectionPool belongPool, SocketStateInPool socketStateInPool = SocketStateInPool.NeverWasInPool )
                : base( remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp )
            {
                Id = remoteEndPoint;
                Pool = belongPool;

                m_canBeTakenFromPool = new AsyncAutoResetEvent( set: false );
                m_lockWaitingToTakeFromPool = new AsyncLock();

                //to always allow init Socket.StateInPool without waiting(see method StateInPool.set)
                m_statusInPool = new StateInPoolReference( SocketStateInPool.NeverWasInPool );

                AllowTakeSocket( socketStateInPool );
            }

            private Socket( EndPoint remoteEndPoint, ConnectionPool belongPool, AsyncAutoResetEvent canBeTakenFromPool, AsyncLock lockWaitingToTakeFromPool, StateInPoolReference socketStateInPool )
                : base( remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp )
            {
                Id = remoteEndPoint;
                Pool = belongPool;

                m_canBeTakenFromPool = canBeTakenFromPool;
                m_lockWaitingToTakeFromPool = lockWaitingToTakeFromPool;

                m_statusInPool = socketStateInPool;
            }

            public EndPoint Id { get; }

            public ConnectionPool Pool { get; }

            public SocketHealth Health
            {
                get
                {
                    SocketHealth socketHealth;

                    try
                    {
                        VerifyConnected();
                    }
                    catch ( SocketException )
                    {
                        socketHealth = SocketHealth.IsNotConnected;
                        return socketHealth;
                    }
                    catch ( ObjectDisposedException )
                    {
                        ;//do nothing, because check whether it is disposed is in ( m_state >= SocketState.Failed )
                    }

                    SocketStateInPool stateInPool = StateInPool;
                    socketHealth = ( stateInPool == SocketStateInPool.IsFailed ) || ( State >= SocketState.Failed ) ?
                        SocketHealth.Expired :
                        SocketHealth.Healthy;

                    return socketHealth;
                }
            }

            internal SocketStateInPool StateInPool =>
                m_statusInPool.Value;

            public override Boolean Equals( Object obj ) =>
                ( obj is Socket socket ) && Id.Equals( socket.Id );

            public override Int32 GetHashCode() =>
                Id.GetHashCode();

            /// <returns>
            /// If <paramref name="bytesToSend"/> is immediately sent, method will return <paramref name="client"/>, else it will return new created <see cref="Socket"/>
            /// </returns>
            public async Task<Socket> DsSendWithAvoidNetworkErrorsAsync( Byte[] bytesToSend,
                TimeSpan timeoutToSend, TimeSpan timeoutToConnect, IoBehavior ioBehavior )
            {
                Socket sendingBytesSocket;
                try
                {
                    await DsSendAsync( bytesToSend, timeoutToSend, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );
                    sendingBytesSocket = this;
                }
                catch ( SocketException e )
                {
                    sendingBytesSocket = await HandleNetworkErrorDuringSendOp( bytesToSend, timeoutToSend, timeoutToConnect, ioBehavior, e ).ConfigureAwait( false );
                }
                catch ( ObjectDisposedException e )
                {
                    sendingBytesSocket = await HandleNetworkErrorDuringSendOp( bytesToSend, timeoutToSend, timeoutToConnect, ioBehavior, e ).ConfigureAwait( false );
                }

                return sendingBytesSocket;
            }

            //public async Task ConnectAsync()
            //{
            //    //check status in lock
            //    //add changing status in all methods
            //    //take work with SslProtocols and TLS
            //    //maybe should to check whether remoteEndPoint is Windows
            //    //check whether remoteEndPoint supports SSL (send message to ask)
            //    //call ConnectAsync with timeout
            //    //change state to connected if it is, otherwise to failed
            //}

            public async ValueTask<Boolean> ReturnToPoolAsync( TimeSpan timeoutToConnect, IoBehavior ioBehavior )
            {
#if CONNECTION_POOL_TEST
                DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Socket with id \"{Id}\" returning to Pool" );
#endif

                if ( Pool == null )
                {
                    return false;
                }
                //we shouldn't check IsInPool because ConnectionPool.semaphoreSocket.Release needed to be called, 
                //because after long time ConnectionPool.semaphoreSocket.CurrentCount can be 0 without this and none can take sockets 
                Boolean isReturned = await Pool.ReturnToPoolAsync( this, timeoutToConnect, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );

                return isReturned;
            }

            public async Task<Boolean> TryRecoverConnectionAsync( Boolean returnToPool, Boolean reuseSocket, TimeSpan disconnectTimeout,
                TimeSpan connectTimeout, IoBehavior ioBehavior, CancellationToken cancellationToken = default )
            {
                Boolean isRecoveredConnection = false;

                Socket newSocket = null;
                try
                {
                    newSocket = NewSimilarSocket( SocketStateInPool.TakenFromPool );
                    await newSocket.DsConnectAsync( newSocket.Id, connectTimeout, ioBehavior, cancellationToken ).ConfigureAwait( false );

                    //if we didn't recover connection, we will have an exception
                    isRecoveredConnection = true;
                }
                catch ( SocketException )
                {
                    ;//do nothing
                }
                catch ( TimeoutException )
                {
                    ;//do nothing
                }
                catch ( ObjectDisposedException )
                {
                    ;//do nothing
                }
                finally
                {
                    if ( returnToPool && ( !cancellationToken.IsCancellationRequested ) )
                    {
                        await newSocket.ReturnToPoolAsync( connectTimeout, ioBehavior ).ConfigureAwait( false );
                    }
                    else if ( cancellationToken.IsCancellationRequested )
                    {
                        try
                        {
                            newSocket.VerifyConnected();

                            await newSocket.DsDisconnectAsync( reuseSocket, disconnectTimeout, cancellationToken ).ConfigureAwait( false );
                        }
                        catch ( SocketException )
                        {
                            ;//do nothing
                        }
                        catch ( TimeoutException )
                        {
                            ;//do nothing
                        }
                        catch ( ObjectDisposedException )
                        {
                            ;//do nothing
                        }
                        finally
                        {
                            newSocket.DisposeUnmanagedResources();
                            await newSocket.ReturnToPoolAsync( connectTimeout, ioBehavior ).ConfigureAwait( false );
                        }
                    }
                }

                return isRecoveredConnection;
            }

            public void DisposeUnmanagedResources()
            {
                try
                {
                    Shutdown( SocketShutdown.Both );
                }
                catch ( ObjectDisposedException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: "Dispose is called several times", ex );
                }
                catch ( SocketException ex )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( ex );
                }
                finally
                {
                    //if we set false, then we will dispose nothing
                    Dispose( disposing: true );
                }
            }

            internal void DisposeUnmanagedResourcesAndSetIsFailed()
            {
                try
                {
                    DisposeUnmanagedResources();
                }
                finally
                {
                    if ( StateInPool != SocketStateInPool.IsFailed )
                    {
                        AllowTakeSocket( SocketStateInPool.IsFailed );
                    }
                }
            }

            internal Socket NewSimilarSocket( SocketStateInPool stateInPool )
            {
                DisposeUnmanagedResources();

                m_statusInPool.Value = stateInPool;
                var newSocket = new Socket( Id, Pool, m_canBeTakenFromPool, m_lockWaitingToTakeFromPool, m_statusInPool );

                return newSocket;
            }

            internal void AllowTakeSocket( SocketStateInPool stateInPool )
            {
                if ( stateInPool != SocketStateInPool.TakenFromPool )
                {
                    SocketStateInPool previousState = StateInPool;

                    m_statusInPool.Value = stateInPool;

                    //if socket was created in pool and taken from there
                    //((previousState == SocketStateInPool.NeverWasInPool) && (value == SocketStateInPool.TakenFromPool)),
                    //then of course we don't have to wait for it to come back to the pool
                    if ( previousState != SocketStateInPool.NeverWasInPool )
                    {
                        m_canBeTakenFromPool.Set();
                    }
                }
                else
                {
                    throw new ArgumentException( message: $"To take socket use method {nameof( TakeFromPoolAsync )} " );
                }
            }

            /// <exception cref="OperationCanceledException">
            /// Cancellation token was requested to cancel
            /// </exception>
            internal async ValueTask TakeFromPoolAsync( IoBehavior ioBehavior, TimeSpan timeWaitSocketReturnedToPool, CancellationToken cancellationToken = default )
            {
                SocketStateInPool previousState = StateInPool;
                if ( previousState != SocketStateInPool.NeverWasInPool )
                {
                    await WaitReturnToPoolByAnotherThreadAsync( ioBehavior, timeWaitSocketReturnedToPool, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
                }

                m_statusInPool.Value = SocketStateInPool.TakenFromPool;
            }

            /// <summary>
            /// Every thread should wait the same time from point when socket is taken(e.g.
            /// if it wasn't so, we can have the situation when
            /// two threads wait to take socket when anyone did that,
            /// the next one will keep waiting and it will be less than Constants.TimeWaitSocketReturnedToPool))
            /// </summary>
            private async ValueTask WaitReturnToPoolByAnotherThreadAsync( IoBehavior ioBehavior, TimeSpan timeWaitSocketReturnedToPool, CancellationToken cancellationToken = default )
            {
                Boolean socketWasReturnedByAnotherThread;

                using ( await m_lockWaitingToTakeFromPool.LockAsync( ioBehavior, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false ) )
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    socketWasReturnedByAnotherThread = await m_canBeTakenFromPool.WaitAsync(
                        timeWaitSocketReturnedToPool,
                        ioBehavior,
                        cancellationToken
                    ).ConfigureAwait( false );
                }

                cancellationToken.ThrowIfCancellationRequested();

                if ( socketWasReturnedByAnotherThread )
                {
#if CONNECTION_POOL_TEST
                    DsLoggerSet.DefaultLogger.LogInfo( logRecord: $"Socket with id {Id} successfully taken from pool".WithAttention() );
#endif
                }
                else
                {
                    String logRecord = $"Taken socket with {Id}, which is used by another thread";
                    var exception = new InvalidProgramException( logRecord );

#if CONNECTION_POOL_TEST
                    throw exception;
#else
                DsLoggerSet.DefaultLogger.LogCriticalError( logRecord, exception );
#endif
                }
            }

            private async Task<Socket> HandleNetworkErrorDuringSendOp( Byte[] bytesToSend,
                TimeSpan timeoutToSend, TimeSpan timeoutToConnect, IoBehavior ioBehavior, Exception exception )
            {
                DsLoggerSet.DefaultLogger.LogError( $"Failed to send message, try only one more: {exception.Message}" );

                Socket sendingBytesSocket = NewSimilarSocket( SocketStateInPool.TakenFromPool );

                await sendingBytesSocket.DsConnectAsync( remoteEndPoint: sendingBytesSocket.Id, timeoutToConnect, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );

                await sendingBytesSocket.DsSendAsync( bytesToSend, timeoutToSend, ioBehavior ).ConfigureAwait( false );
                return sendingBytesSocket;
            }
        }
    }
}
