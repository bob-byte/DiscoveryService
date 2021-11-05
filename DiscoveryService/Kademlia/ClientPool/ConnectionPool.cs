using LUC.DiscoveryService.Common;
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

namespace LUC.DiscoveryService.Kademlia.ClientPool
{
    sealed partial class ConnectionPool
    {
        private const UInt32 POOL_RECOVERY_FREQUENCY_IN_TICKS = 1000;
        
        private static ConnectionPool s_instance;

        private readonly ILoggingService m_log;

        private readonly SemaphoreSlim m_cleanSemaphore;
        private readonly SemaphoreSlim m_socketSemaphore;
        private readonly SemaphoreLocker m_lockTakeSocket;
        private readonly Object m_lockLastRecoveryTime;

        //We use ConcurrentDictionary and lockers, because sometimes we have intermidiate steps 
        //between using these dictionaries and we use this type for more security
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

            m_lockLastRecoveryTime = new Object();
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

        public async Task<ConnectionPoolSocket> SocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool )
        {
#if DEBUG
            // wait for an open slot (until timeout return to pool is raised)
            m_log.LogInfo( "Pool waiting for a taking from the pool" );
#endif
            //"_", because no matter whether socket is returned to the pool by the another thread
            _ = await CanSocketBeTakenFromPoolAsync( ioBehavior, m_socketSemaphore, timeWaitToReturnToPool ).
                ConfigureAwait(continueOnCapturedContext: false);

            //we need to try to take socket also here in order to don't wait while m_lockTakeSocket is released
            TryTakeLeasedSocket( remoteEndPoint, timeWaitToReturnToPool, out ConnectionPoolSocket desiredSocket );
            if ( desiredSocket != null )
            {
                desiredSocket = await TakenSocketWithRecoveredConnectionAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).
                    ConfigureAwait(false);
            }
            else
            {
                desiredSocket = await m_lockTakeSocket.LockAsync( async () =>
                {
                    return await CreatedOrTakenSocketAsync(
                        remoteEndPoint,
                        timeoutToConnect,
                        ioBehavior,
                        timeWaitToReturnToPool
                    ).ConfigureAwait( false );

                } ).ConfigureAwait( false );
            }            

            return desiredSocket;
        }

        private async Task<ConnectionPoolSocket> CreatedOrTakenSocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool )
        {
            TryTakeLeasedSocket( remoteEndPoint, timeWaitToReturnToPool, out ConnectionPoolSocket desiredSocket );

            //another thread can remove desiredSocket from m_leasedSockets, so it can be null
            if ( desiredSocket != null )
            {
                desiredSocket = await TakenSocketWithRecoveredConnectionAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).
                    ConfigureAwait( false );
            }

            Boolean takenFromPool = desiredSocket != null;
            if ( !takenFromPool )
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

                Boolean isAdded = m_leasedSockets.TryAdd( remoteEndPoint, desiredSocket );
                if(!isAdded)
                {
                    String logRecord = Display.StringWithAttention( "Taken socket which is used by another thread" );
                    m_log.LogInfo( logRecord );
                }
            }

            return desiredSocket;
        }

        private Boolean TryTakeLeasedSocket( EndPoint remoteEndPoint, TimeSpan timeWaitToReturnToPool, out ConnectionPoolSocket desiredSocket )
        {
            Boolean takenSocket = m_leasedSockets.TryGetValue( remoteEndPoint, out desiredSocket );

            if ( takenSocket )
            {
                Boolean isReturned = desiredSocket.CanBeTakenFromPool.WaitOne( timeWaitToReturnToPool );
                if ( isReturned )
                {
                    m_log.LogInfo( logRecord: Display.StringWithAttention( $"Socket with id {desiredSocket.Id} successfully taken from pool" ) );
                }
                else
                {
                    //case when BackgroundConnectionResetHelper wait to start reset connections
#if DEBUG
                    ThrowConcurrencyException.ThrowWithConnectionPoolSocketDescr( desiredSocket );
#else
                    Log.LogError( Display.StringWithAttention( logRecord: $"Socket with id {Id} isn\'t returned to pool by some thread" );
#endif
                }
            }

            return takenSocket;
        }

        private async Task<ConnectionPoolSocket> ConnectedSocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, ConnectionPoolSocket socket, CancellationToken cancellationToken = default )
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
            }
            else
            {
                connectedSocket = new ConnectionPoolSocket( remoteEndPoint, s_instance, m_log );
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
        private async Task<Boolean> CanSocketBeTakenFromPoolAsync( IOBehavior ioBehavior, SemaphoreSlim semaphoreSlim, TimeSpan waitTimeout )
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
                        //m_sockets.AddOrUpdate( remoteEndPoint, socketInPool, updateValueFactory: (socketId, oldSocket) => socketInPool );
                        m_sockets.TryAdd( remoteEndPoint, socketInPool );

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
                m_socketSemaphore.Release();
            }

            return isReturned;
        }

        public async Task ClearPoolAsync( IOBehavior ioBehavior, Boolean respectMinPoolSize, CancellationToken cancellationToken )
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
                TimeSpan waitTimeout = TimeSpan.FromMilliseconds( value: 10 );
                while ( true )
                {
                    // if respectMinPoolSize is true, return if (leased sessions + waiting sessions <= minPoolSize)
                    if ( respectMinPoolSize )
                    {
                        if ( ConnectionSettings.MaximumPoolSize - m_socketSemaphore.CurrentCount + m_sockets.Count <= ConnectionSettings.MinimumPoolSize )
                        {
                            return;
                        }
                    }

                    try
                    {
                        KeyValuePair<EndPoint, ConnectionPoolSocket> waitingSocket;

                        //this LINQ method is not thread safe
                        lock ( m_sockets)
                        {
                            waitingSocket = m_sockets.FirstOrDefault();
                        }

                        Boolean isInPoolAnySocket;
                        try
                        {
                            isInPoolAnySocket = !waitingSocket.Equals( default( KeyValuePair<EndPoint, ConnectionPoolSocket> ) );
                        }
                        catch(ObjectDisposedException)
                        {
                            isInPoolAnySocket = true;
                        }

                        if ( isInPoolAnySocket )
                        {
                            waitingSocket.Value.Dispose();
                        }
                        else
                        {
                            return;
                        }
                    }
                    finally
                    {
                        m_socketSemaphore.Release();
                    }
                }
            }
            finally
            {
                m_cleanSemaphore.Release();
                BackgroundConnectionResetHelper.Stop();
            }
        }
    }
}
