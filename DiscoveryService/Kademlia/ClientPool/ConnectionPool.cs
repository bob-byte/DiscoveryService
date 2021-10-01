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
        private readonly Object m_lockLastRecoveryTime;

        private readonly ConcurrentDictionary<EndPoint, ConnectionPoolSocket> m_sockets;
        private readonly ConcurrentDictionary<EndPoint, ConnectionPoolSocket> m_leasedSockets;

        private UInt32 m_lastRecoveryTimeInTicks;

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
            m_recoverSocketsSemaphore = new SemaphoreSlim( initialCount: 1, maxCount: 1 );
            m_lockLastRecoveryTime = new Object();

            m_sockets = new ConcurrentDictionary<EndPoint, ConnectionPoolSocket>();
            m_leasedSockets = new ConcurrentDictionary<EndPoint, ConnectionPoolSocket>();

            m_cancellationRecover = new CancellationTokenSource();

            m_maxTimeWaitEndPreviousRecovering = Constants.ConnectTimeout;
        }

        public ConnectionSettings ConnectionSettings { get; }

        /// <summary>
		/// Returns <c>true</c> if the connection pool is empty, i.e., all connections are in use. Note that in a highly-multithreaded
		/// environment, the value of this property may be stale by the time it's returned.
		/// </summary>
		internal Boolean IsEmpty => m_socketSemaphore.CurrentCount == 0;

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
            if ( IsEmpty && ( unchecked(( (UInt32)Environment.TickCount ) - m_lastRecoveryTimeInTicks) >= POOL_RECOVERY_FREQUENCY_IN_TICKS ) )
            {
                TryCancelRecoverConnections();

                if(ioBehavior == IOBehavior.Asynchronous)
                {
                    await TryRecoverAllConnectionsAsync( timeWaitToReturnToPool ).
                        ConfigureAwait( continueOnCapturedContext: false );
                }
                else if ( ioBehavior == IOBehavior.Synchronous )
                {
                    Task taskRecoverConnections = TryRecoverAllConnectionsAsync( timeWaitToReturnToPool );
                    taskRecoverConnections.Wait();
                }
            }

            // wait for an open slot (until timeout return to pool is raised)
#if DEBUG
            m_log.LogInfo( "Pool waiting for a taking from the pool" );
#endif
            //"_", because no matter whether socket is returned to the pool by the another thread
            _ = await IsSocketReturnedToPoolAsync( ioBehavior, m_socketSemaphore, timeWaitToReturnToPool );

            ConnectionPoolSocket desiredSocket = null;
            if ( m_leasedSockets.ContainsKey( remoteEndPoint ) )
            {
                desiredSocket = await TakeLeasedSocket( remoteEndPoint, ioBehavior, timeWaitToReturnToPool ).ConfigureAwait( false );
                desiredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).
                    ConfigureAwait( false );
            }

            Boolean takenFromPool = desiredSocket != null;
            if ( !takenFromPool )
            {
                Boolean wasInPool = m_sockets.TryRemove( remoteEndPoint, out desiredSocket );
                try
                {
                    desiredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).
                        ConfigureAwait( false );
                }
                finally
                {
                    if(desiredSocket != null)
                    {
                        desiredSocket.IsInPool = false;
                    }
                }

                m_leasedSockets.TryAdd( remoteEndPoint, desiredSocket );
            }

            return desiredSocket;
        }

        private async ValueTask<ConnectionPoolSocket> TakeLeasedSocket( EndPoint remoteEndPoint, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool )
        {
            ConnectionPoolSocket desiredSocket = null;
            try
            {
                desiredSocket = m_leasedSockets[ remoteEndPoint ];
            }
            //case when another thread remove socket with desiredSocket.Id from leasedSockets
            catch ( KeyNotFoundException )
            {
                ;//do nothing, because desiredSocket will be created in method ConnectedSocketAsync even the first one is null
            }

            if(desiredSocket != null)
            {
                _ = await IsSocketReturnedToPoolAsync( ioBehavior, desiredSocket.ReturnedInPool, timeWaitToReturnToPool ).ConfigureAwait( false );
            }

            return desiredSocket;
        }

        private async ValueTask<ConnectionPoolSocket> ConnectedSocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, ConnectionPoolSocket socket )
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
                        await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior ).
                            ConfigureAwait( continueOnCapturedContext: false );
                    }
                    catch (TimeoutException ex)
                    {
                        m_socketSemaphore.Release();

                        m_log.LogError( ex.ToString() );
                        throw;
                    }
                    catch ( SocketException )
                    {
                        try
                        {
                            connectedSocket = new ConnectionPoolSocket( SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, s_instance, m_log );
                            await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior ).
                                ConfigureAwait( false );
                        }
                        catch ( TimeoutException ex )
                        {
                            m_socketSemaphore.Release();

                            m_log.LogError( ex.ToString() );
                            throw;
                        }
                    }
                }
            }
            else
            {
                connectedSocket = new ConnectionPoolSocket( SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, s_instance, m_log );
                try
                {
                    await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior ).
                        ConfigureAwait( false );
                }
                catch ( TimeoutException )
                {
                    m_socketSemaphore.Release();
                    throw;
                }
            }

            return connectedSocket;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ioBehavior"></param>
        /// <param name="semaphoreSlim"></param>
        /// <param name="waitTimeout"></param>
        /// <returns>
        /// It will return <a href="true"/> if socket is returned by another thread during <paramref name="waitTimeout"/>.
        /// It will return <a href="false"/> if socket isn't returned
        /// </returns>
        private async ValueTask<Boolean> IsSocketReturnedToPoolAsync( IOBehavior ioBehavior, SemaphoreSlim semaphoreSlim, TimeSpan waitTimeout )
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
                    SocketHealth socketHealth = socketInPool.SocketHealth( ConnectionSettings );
                    if ( socketHealth == SocketHealth.Healthy )
                    {
                        m_sockets.TryAdd( remoteEndPoint, socketInPool );

                        socketInPool.IsInPool = true;
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

                    // try to get an open slot; if this fails, connection pool is full and sessions will be disposed when returned to pool
                    if ( ioBehavior == IOBehavior.Asynchronous )
                    {
                        if ( !await m_socketSemaphore.WaitAsync( waitTimeout, cancellationToken ).ConfigureAwait( false ) )
                        {
                            return;
                        }
                    }
                    else
                    {
                        if ( !m_socketSemaphore.Wait( waitTimeout, cancellationToken ) )
                        {
                            return;
                        }
                    }

                    try
                    {
                        // check for a waiting session
                        KeyValuePair<EndPoint, ConnectionPoolSocket> waitingSocket = m_sockets.FirstOrDefault();
                        if ( !waitingSocket.Equals( default( KeyValuePair<EndPoint, ConnectionPoolSocket> ) ) )
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
