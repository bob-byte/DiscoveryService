using LUC.DiscoveryServices.Common;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    sealed partial class ConnectionPool
    {
        private static ConnectionPool s_instance;

        private readonly ILoggingService m_log;

        private readonly SemaphoreSlim m_cleanSemaphore;
        private readonly SemaphoreSlim m_socketSemaphore;
        private readonly SemaphoreLocker m_lockTakeSocket;

        private readonly ConcurrentDictionary<EndPoint, ConnectionPoolSocket> m_sockets;
        private readonly ConcurrentDictionary<EndPoint, ConnectionPoolSocket> m_leasedSockets;

        private ConnectionPool( ConnectionSettings connectionSettings )
        {
            m_log = new LoggingService
            {
                SettingsService = new SettingsService()
            };

            ConnectionSettings = connectionSettings;
            if(ConnectionSettings.ConnectionReset)
            {
                BackgroundConnectionResetHelper.Start();
            }

            m_cleanSemaphore = new SemaphoreSlim( initialCount: 1 );
            m_socketSemaphore = new SemaphoreSlim( connectionSettings.MaximumPoolSize );

            m_lockTakeSocket = new SemaphoreLocker();

            m_sockets = new ConcurrentDictionary<EndPoint, ConnectionPoolSocket>();
            m_leasedSockets = new ConcurrentDictionary<EndPoint, ConnectionPoolSocket>();

            m_cancellationRecover = new CancellationTokenSource();
        }

        public ConnectionSettings ConnectionSettings { get; }

        public static ConnectionPool Instance()
        {
            if ( s_instance == null )
            {
                s_instance = new ConnectionPool( new ConnectionSettings() );
            }

            return s_instance;
        }

        public async ValueTask<ConnectionPoolSocket> SocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool )
        {
#if DEBUG
            // wait for an open slot (until timeout return to pool is raised)
            m_log.LogInfo( "Pool waiting for a taking from the pool" );
#endif
            //"_", because no matter whether socket is returned to the pool by the another thread
            _ = await CanSocketBeTakenFromPoolAsync( ioBehavior, m_socketSemaphore, timeWaitToReturnToPool )
                .ConfigureAwait( continueOnCapturedContext: false );

            //we need to try to take socket also here in order to don't wait while m_lockTakeSocket is released
            Boolean isTaken = TryTakeLeasedSocket( remoteEndPoint, out ConnectionPoolSocket desiredSocket );
            if ( isTaken )
            {
                desiredSocket = await TakenSocketWithRecoveredConnectionAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket )
                    .ConfigureAwait( false );
            }
            else
            {
                if ( ioBehavior == IOBehavior.Asynchronous )
                {
                    desiredSocket = await m_lockTakeSocket.LockAsync( async () =>
                    {
                        return await CreatedOrTakenSocketAsync(
                            remoteEndPoint,
                            timeoutToConnect,
                            ioBehavior
                        ).ConfigureAwait( false );

                    } ).ConfigureAwait( false );
                }
                else if ( ioBehavior == IOBehavior.Synchronous )
                {
                    desiredSocket = await m_lockTakeSocket.Lock( async () =>
                    {
                        return await CreatedOrTakenSocketAsync(
                            remoteEndPoint,
                            timeoutToConnect,
                            ioBehavior
                        ).ConfigureAwait( false );

                    } ).ConfigureAwait( false );
                }
                else
                {
                    throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
                }
            }            

            return desiredSocket;
        }

        private async ValueTask<ConnectionPoolSocket> CreatedOrTakenSocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior )
        {
            Boolean isTaken = TryTakeLeasedSocket( remoteEndPoint, out ConnectionPoolSocket desiredSocket );

            if ( isTaken )
            {
                desiredSocket = await TakenSocketWithRecoveredConnectionAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).
                    ConfigureAwait( false );
            }
            else
            {
                //desiredSocket may not be in pool, because it can be removed 
                //by ConnectionPool.TryRecoverAllConnectionsAsync or disposed or is not created
                m_sockets.TryRemove( remoteEndPoint, out desiredSocket );

                desiredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).
                    ConfigureAwait( false );

                if ( desiredSocket.StateInPool != SocketStateInPool.TakenFromPool )
                {
                    desiredSocket.StateInPool = SocketStateInPool.TakenFromPool;
                }

                Boolean isUpdated = false;
                m_leasedSockets.AddOrUpdate( remoteEndPoint, (socketId) => desiredSocket, (socketId, previousSocketValue) =>
                {
                    isUpdated = true;
                    return desiredSocket;
                } );

                if(isUpdated)
                {
                    String logRecord = Display.StringWithAttention( "Taken socket which is used by another thread" );
                    m_log.LogInfo( logRecord );
                }
            }

            return desiredSocket;
        }

        private Boolean TryTakeLeasedSocket( EndPoint remoteEndPoint, out ConnectionPoolSocket desiredSocket )
        {
            Boolean takenSocket = m_leasedSockets.TryGetValue( remoteEndPoint, out desiredSocket );

            if ( takenSocket )
            {
                desiredSocket.StateInPool = SocketStateInPool.TakenFromPool;
                AddLeasedSocket( remoteEndPoint, desiredSocket );
            }

            return takenSocket;
        }

        private void AddLeasedSocket(EndPoint socketId, ConnectionPoolSocket socket) =>
            m_leasedSockets.AddOrUpdate( socketId, ( key ) => socket, ( key, previousSocketValue ) => socket );

        private void ReleaseSocketSemaphore()
        {
            if ( m_socketSemaphore.CurrentCount < ConnectionSettings.MaximumPoolSize )
            {
                m_socketSemaphore.Release();
            }
        }

        private async ValueTask<ConnectionPoolSocket> ConnectedSocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, ConnectionPoolSocket socket, CancellationToken cancellationToken = default )
        {
            ConnectionPoolSocket connectedSocket = socket;
            if ( socket != null )
            {
                try
                {
                    connectedSocket.VerifyConnected();
                }
                catch ( SocketException )
                {
                    try
                    {
                        await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken ).
                            ConfigureAwait( continueOnCapturedContext: false );
                    }
                    catch ( SocketException )
                    {
                        try
                        {
                            connectedSocket = new ConnectionPoolSocket( remoteEndPoint, s_instance, m_log, socket.StateInPool );
                            await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken ).
                                ConfigureAwait( false );
                        }
                        catch ( SocketException ex )
                        {
                            HandleRemoteException( ex, connectedSocket );
                        }
                        catch ( TimeoutException ex )
                        {
                            HandleRemoteException( ex, connectedSocket );
                        }
                    }
                    catch ( TimeoutException ex )
                    {
                        HandleRemoteException( ex, connectedSocket );
                    }
                    catch ( ObjectDisposedException ex )
                    {
                        HandleRemoteException( ex, connectedSocket );
                    }
                }
                catch(ObjectDisposedException)
                {
                    connectedSocket = await CreatedConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken ).ConfigureAwait(false);
                }
            }
            else
            {
                connectedSocket = await CreatedConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken ).ConfigureAwait( false );
            }

            return connectedSocket;
        }

        private async ValueTask<ConnectionPoolSocket> CreatedConnectedSocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, CancellationToken cancellationToken = default )
        {
            ConnectionPoolSocket connectedSocket = new ConnectionPoolSocket( remoteEndPoint, s_instance, m_log );
            try
            {
                await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken ).
                    ConfigureAwait( false );
            }
            catch ( SocketException ex )
            {
                HandleRemoteException( ex, connectedSocket );
            }
            catch ( TimeoutException ex )
            {
                HandleRemoteException( ex, connectedSocket );
            }

            return connectedSocket;
        }

        private void HandleRemoteException(Exception exception, ConnectionPoolSocket failedSocket = null)
        {
            //if(!(exception is TimeoutException) && !(exception is SocketException))
            //{
            //    m_log.LogError( exception.ToString() );
            //}

            if(failedSocket != null)
            {
                failedSocket.StateInPool = SocketStateInPool.IsFailed;
            }

            m_socketSemaphore.Release();
            throw exception;
        }

        /// <returns>
        /// It will return <a href="true"/> if socket is returned by another thread during <paramref name="waitTimeout"/>.
        /// It will return <a href="false"/> if socket isn't returned
        /// </returns>
        private async ValueTask<Boolean> CanSocketBeTakenFromPoolAsync( IOBehavior ioBehavior, SemaphoreSlim semaphoreSlim, TimeSpan waitTimeout )
        {
            Boolean successWait;
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                successWait = await semaphoreSlim.WaitAsync( waitTimeout ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IOBehavior.Synchronous )
            {
                successWait = semaphoreSlim.Wait( waitTimeout );
            }
            else
            {
                throw new ArgumentException( $"{nameof( ioBehavior )} has incorrect value" );
            }

            return successWait;
        }

        public Boolean ReturnedToPool( EndPoint remoteEndPoint )
        {
#if DEBUG
            m_log.LogInfo( $"Pool receiving Session with id {remoteEndPoint} back" );
#endif

            Boolean isReturned = false;
            try
            {
                Boolean wasInPool = m_leasedSockets.TryRemove( remoteEndPoint, out ConnectionPoolSocket socketInPool );

                if ( wasInPool )
                {
                    SocketHealth socketHealth = socketInPool.SocketHealth();
                    if ( socketHealth == SocketHealth.Healthy )
                    {
                        m_sockets.AddOrUpdate( remoteEndPoint, socketInPool, updateValueFactory: ( socketId, oldSocket ) => socketInPool );

                        socketInPool.StateInPool = SocketStateInPool.IsInPool;
                        isReturned = true;
                    }
                    else
                    {
                        if ( socketHealth == SocketHealth.IsNotConnected )
                        {
                            m_log.LogInfo( $"Pool received invalid Socket {socketInPool.Id}; destroying it" );
                        }
                        else if ( socketHealth == SocketHealth.Expired )
                        {
                            m_log.LogInfo( $"Pool received expired Socket {socketInPool.Id}; destroying it" );
                        }

                        if ( socketInPool.State < SocketState.Closing )
                        {
                            socketInPool.Dispose();
                        }

                        isReturned = false;
                    }
                }
                else
                {
                    isReturned = false;
                }
            }
            finally
            {
                ReleaseSocketSemaphore();
            }

            return isReturned;
        }

        public async ValueTask ClearPoolAsync( IOBehavior ioBehavior, Boolean respectMinPoolSize, CancellationToken cancellationToken )
        {
            m_log.LogInfo( $"Pool clearing connection pool" );

            // synchronize access to this method as only one clean routine should be run at a time
            if ( ioBehavior == IOBehavior.Asynchronous )
            {
                await m_cleanSemaphore.WaitAsync( cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else
            {
                m_cleanSemaphore.Wait( cancellationToken );
            }

            try
            {
                CleanSockets( respectMinPoolSize, m_sockets );
                CleanSockets( respectMinPoolSize, m_leasedSockets );
            }
            finally
            {
                m_cleanSemaphore.Release();
                BackgroundConnectionResetHelper.Stop();
            }
        }

        private void CleanSockets( Boolean respectMinPoolSize, ConcurrentDictionary<EndPoint, ConnectionPoolSocket> sockets )
        {
            Boolean canAnySocketBeRemoved = sockets.Count > 0;
            while ( canAnySocketBeRemoved )
            {
                CleanOneSocket( respectMinPoolSize, sockets, out canAnySocketBeRemoved );
            }
        }

        private void CleanOneSocket( Boolean respectMinPoolSize, ConcurrentDictionary<EndPoint, ConnectionPoolSocket> sockets, out Boolean removedAnySocket )
        {
            // if respectMinPoolSize is true, return if (leased sessions + waiting sessions <= minPoolSize)
            if ( respectMinPoolSize )
            {
                if ( ConnectionSettings.MaximumPoolSize - m_socketSemaphore.CurrentCount + m_sockets.Count <= ConnectionSettings.MinimumPoolSize )
                {
                    removedAnySocket = false;
                    return;
                }
            }

            KeyValuePair<EndPoint, ConnectionPoolSocket> waitingSocket;

            //this LINQ method is not thread safe
            lock ( sockets )
            {
                waitingSocket = sockets.FirstOrDefault();
            }

            try
            {
                removedAnySocket = !waitingSocket.Equals( default( KeyValuePair<EndPoint, ConnectionPoolSocket> ) );
            }
            catch ( ObjectDisposedException )
            {
                removedAnySocket = true;
            }

            if ( removedAnySocket )
            {
                sockets.TryRemove( waitingSocket.Key, value: out _ );

                if ( waitingSocket.Value.StateInPool == SocketStateInPool.TakenFromPool )
                {
                    ReleaseSocketSemaphore();
                }

                if ( waitingSocket.Value.State < SocketState.Closing )
                {
                    waitingSocket.Value.Dispose();
                }
            }
        }
    }
}
