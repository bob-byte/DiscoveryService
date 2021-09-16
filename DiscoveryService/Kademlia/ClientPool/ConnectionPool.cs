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

namespace LUC.DiscoveryService.Kademlia.ClientPool
{
    sealed class ConnectionPool
    {
        private const UInt32 POOL_RECOVERY_FREQUENCY_IN_MS = 1000;

        private static ConnectionPool s_instance;
        private readonly ILoggingService m_log;

        private readonly SemaphoreSlim m_cleanSemaphore;
        private readonly SemaphoreSlim m_socketSemaphore;
        private readonly Object m_lockLastRecoveryTime;

        private readonly ConcurrentDictionary<EndPoint, ConnectionPoolSocket> m_sockets;
        private readonly ConcurrentDictionary<EndPoint, ConnectionPoolSocket> m_leasedSockets;

        private UInt32 m_lastRecoveryTime;

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

            m_sockets = new ConcurrentDictionary<EndPoint, ConnectionPoolSocket>();
            m_leasedSockets = new ConcurrentDictionary<EndPoint, ConnectionPoolSocket>();
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
            if ( IsEmpty && ( unchecked(( (UInt32)Environment.TickCount ) - m_lastRecoveryTime) >= POOL_RECOVERY_FREQUENCY_IN_MS ) )
            {
                m_log.LogInfo( "Pool is empty; recovering leaked sockets" );

                await RecoverLeakedSocketsAsync( ioBehavior, timeoutToConnect ).ConfigureAwait( continueOnCapturedContext: false );
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
                desiredSocket = await TakeLeasedSocket( remoteEndPoint, ioBehavior, timeWaitToReturnToPool, timeoutToConnect ).ConfigureAwait( false );
            }

            Boolean takenFromPool = desiredSocket != null;
            if ( !takenFromPool )
            {
                m_sockets.TryRemove( remoteEndPoint, out desiredSocket );

                desiredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).ConfigureAwait( false );

                m_leasedSockets.TryAdd( remoteEndPoint, desiredSocket );
                desiredSocket.IsInPool = false;
            }

            return desiredSocket;
        }

        private async ValueTask<ConnectionPoolSocket> TakeLeasedSocket( EndPoint remoteEndPoint, IOBehavior ioBehavior, TimeSpan timeWaitToReturnToPool, TimeSpan timeoutToConnect )
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
                Boolean isSocketReturned = await IsSocketReturnedToPoolAsync( ioBehavior, desiredSocket.ReturnedInPool, timeWaitToReturnToPool ).ConfigureAwait( false );

                if(!isSocketReturned)
                {
                    desiredSocket = null;
                }
            }

            desiredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket ).ConfigureAwait( false );

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
                        await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior ).ConfigureAwait( continueOnCapturedContext: false );
                    }
                    catch ( SocketException )
                    {
                        connectedSocket = new ConnectionPoolSocket( SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, s_instance, m_log );
                        await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior ).ConfigureAwait( false );
                    }
                }
            }
            else
            {
                connectedSocket = new ConnectionPoolSocket( SocketType.Stream, ProtocolType.Tcp, remoteEndPoint, s_instance, m_log );
                await connectedSocket.DsConnectAsync( remoteEndPoint, timeoutToConnect, ioBehavior ).ConfigureAwait( false );
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

        /// <summary>
		/// Examines all the <see cref="ServerSession"/> objects in <see cref="m_leasedSockets"/> to determine if any
		/// have an owning <see cref="MySqlConnection"/> that has been garbage-collected. If so, assumes that the connection
		/// was not properly disposed and returns the session to the pool.
		/// </summary>
		private async ValueTask RecoverLeakedSocketsAsync( IOBehavior ioBehavior, TimeSpan timeoutToConnect )
        {
            List<ConnectionPoolSocket> recoveredSockets = new List<ConnectionPoolSocket>();

            lock ( m_lockLastRecoveryTime )
            {
                m_lastRecoveryTime = unchecked((UInt32)Environment.TickCount);
            }

            foreach ( ConnectionPoolSocket socket in m_leasedSockets.Values )
            {
                try
                {
                    ConnectionPoolSocket restoredSocket = await ConnectedSocketAsync(
                        socket.Id,
                        timeoutToConnect,
                        ioBehavior,
                        socket
                    ).ConfigureAwait( continueOnCapturedContext: false );
                    restoredSocket.VerifyConnected();

                    recoveredSockets.Add( restoredSocket );
                }
                //if recoveredSocket is not connected, SocketException will occur
                catch ( SocketException )
                {
                    ;//do nothing
                }
                catch ( TimeoutException )
                {
                    ;//do nothing
                }
            }

            if ( recoveredSockets.Count == 0 )
            {
#if DEBUG
                m_log.LogInfo( $"Pool recovered no sockets" );
#endif
            }
            else
            {
                m_log.LogInfo( $"Pool now recovers socket count = {recoveredSockets.Count}" );
            }

            foreach ( ConnectionPoolSocket socket in recoveredSockets )
            {
                socket.ReturnedToPool();
            }
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
