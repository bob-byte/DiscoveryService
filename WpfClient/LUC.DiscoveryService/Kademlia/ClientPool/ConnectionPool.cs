using LUC.DiscoveryServices.Common;
using LUC.DiscoveryServices.Common.Extensions;
using LUC.Interfaces.Helpers;
using LUC.Services.Implementation.Helpers;

using Nito.AsyncEx;

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    /// <summary>
    /// Implements <a href="https://csharpindepth.com/Articles/Singleton">singleton pattern</a>
    /// </summary>
    internal sealed partial class ConnectionPool
    {
        private static ConnectionPool s_instance;

        private readonly SemaphoreSlim m_socketsSemaphore;
        private readonly AsyncLock m_lockTakeSocket;

        private readonly ConcurrentDictionary<EndPoint, Socket> m_sockets;
        private readonly ConcurrentDictionary<EndPoint, Socket> m_leasedSockets;

        private readonly ImmutableDictionary<Boolean, ValueTask<Boolean>> m_cachedValueTasks;

        private ConnectionPool( ConnectionSettings connectionSettings )
        {
            ConnectionSettings = connectionSettings;
            if ( ConnectionSettings.ConnectionBackgroundReset )
            {
                BackgroundConnectionResetHelper.Start();
            }

            m_cleanSemaphore = new SemaphoreSlim( initialCount: 1 );
            m_socketsSemaphore = new SemaphoreSlim( connectionSettings.MaxCountSocketInUse );

            m_lockTakeSocket = new AsyncLock();

            m_sockets = new ConcurrentDictionary<EndPoint, Socket>();
            m_leasedSockets = new ConcurrentDictionary<EndPoint, Socket>();

            //create readonly cached value tasks
            ImmutableDictionary<Boolean, ValueTask<Boolean>>.Builder builder =
                ImmutableDictionary.CreateBuilder<Boolean, ValueTask<Boolean>>();
            builder.Add( key: true, value: new ValueTask<Boolean>( result: true ) );
            builder.Add( false, new ValueTask<Boolean>( false ) );
            m_cachedValueTasks = builder.ToImmutableDictionary();

            m_cancellationRecover = new CancellationTokenSource();
        }

        public static ConnectionPool Instance
        {
            get
            {
                SingletonInitializer.ThreadSafeInit( value: () => new ConnectionPool( new ConnectionSettings() ), ref s_instance );
                return s_instance;
            }
        }

        public ConnectionSettings ConnectionSettings { get; }

        public ValueTask<Socket> SocketAsync( EndPoint remoteEndPoint, IoBehavior ioBehavior, TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken = default ) =>
            SocketAsync( remoteEndPoint, ConnectionSettings.ConnectionTimeout, ioBehavior, timeWaitToReturnToPool, cancellationToken );

        public async ValueTask<Socket> SocketAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IoBehavior ioBehavior, TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken = default )
        {
#if CONNECTION_POOL_TEST
            // wait for an open slot (until timeout return to pool is raised)
            DsLoggerSet.DefaultLogger.LogInfo( "Pool waiting for a taking from the pool" );
#endif
            //"_", because no matter whether socket is returned to the pool by the another thread
            Boolean canSocketBeTaken = await CanSocketBeTakenFromPoolAsync( ioBehavior, timeWaitToReturnToPool )
                .ConfigureAwait( continueOnCapturedContext: false );
            if ( canSocketBeTaken )
            {
                //we need to try to take socket also here in order to don't wait while m_lockTakeSocket is released
                (Boolean isTaken, Socket desiredSocket) = await TryTakeLeasedSocketAsync(
                    remoteEndPoint,
                    ioBehavior,
                    timeWaitToReturnToPool,
                    cancellationToken
                ).ConfigureAwait( false );

                if ( isTaken )
                {
                    desiredSocket = await TakenSocketWithRecoveredConnectionAsync(
                        remoteEndPoint,
                        timeoutToConnect,
                        ioBehavior,
                        desiredSocket
                    ).ConfigureAwait( false );
                }
                else
                {
                    //the socket with the same ID can be taken from the pool by another
                    //thread while current was waiting for releasing m_lockTakeSocket
                    using ( await m_lockTakeSocket.LockAsync( ioBehavior ).ConfigureAwait( false ) )
                    {
                        desiredSocket = await CreatedOrTakenSocketAsync(
                            remoteEndPoint,
                            timeoutToConnect,
                            timeWaitToReturnToPool,
                            ioBehavior
                        ).ConfigureAwait( false );
                    }
                }

                return desiredSocket;
            }
            else
            {
                throw new InvalidOperationException( message: "Too many threads use pool." );
            }
        }

        public ValueTask<Boolean> ReturnToPoolAsync( Socket socket, IoBehavior ioBehavior ) =>
            ReturnToPoolAsync( socket, ConnectionSettings.ConnectionTimeout, ioBehavior );

        public async ValueTask<Boolean> ReturnToPoolAsync( Socket socket, TimeSpan timeoutToConnect, IoBehavior ioBehavior )
        {
#if CONNECTION_POOL_TEST
            DsLoggerSet.DefaultLogger.LogInfo( $"Pool receiving Session with id {socket.Id} back" );
#endif

            Boolean isReturned = false;
            Boolean wasTakenFromPool = m_leasedSockets.ContainsKey( socket.Id );

            try
            {
                if ( wasTakenFromPool )
                {
                    SocketHealth socketHealth = socket.Health;
                    Boolean isConnected;

                    if ( socketHealth == SocketHealth.Healthy )
                    {
                        isConnected = true;
                    }
                    else
                    {
                        try
                        {
                            socket = await ConnectedSocketAsync(
                                socket.Id,
                                timeoutToConnect,
                                ioBehavior,
                                socket,
                                createNewSocketIfDisposed: true,
                                handleRemoteException: false
                            ).ConfigureAwait( false );

                            //we will have an exception if unable to connect
                            isConnected = true;
                        }
                        catch ( SocketException )
                        {
                            isConnected = false;
                        }
                        catch ( TimeoutException )
                        {
                            isConnected = false;
                        }
                        catch ( InvalidOperationException )
                        {
                            isConnected = false;
                        }
                    }

                    SocketStateInPool currentStateInPool;
                    if ( isConnected )
                    {
                        currentStateInPool = SocketStateInPool.IsInPool;
                    }
                    else
                    {
                        socket.DisposeUnmanagedResources();
                        currentStateInPool = SocketStateInPool.IsFailed;
                    }

                    InternalReturnSocket( socket, currentStateInPool, out isReturned );
                }
            }
            finally
            {
                ReleaseSocketSemaphore();

                if ( !isReturned )
                {
                    NotifyPoolError( message: 
                        "Try to return socket which already removed from pool" );
                }
            }

            return isReturned;
        }

        private async ValueTask TakeFromPoolAsync( Socket socket, IoBehavior ioBehavior, TimeSpan timeWaitSocketReturnedToPool, CancellationToken cancellationToken = default )
        {
            await socket.TakeFromPoolAsync( ioBehavior, timeWaitSocketReturnedToPool, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
        }

        private void HandleRemoteException( Exception exception, Socket failedSocket = null )
        {
            try
            {
                failedSocket?.AllowTakeSocket( SocketStateInPool.IsFailed );
            }
            catch ( InvalidOperationException )
            {
                ;//do nothing
            }

            ReleaseSocketSemaphore();
            throw exception;
        }

        private void NotifyPoolError( String message )
        {
            var exception = new InvalidProgramException( message );

#if CONNECTION_POOL_TEST
            throw exception;
#else
            DsLoggerSet.DefaultLogger.LogCriticalError( message, exception );
#endif
        }

        private void InternalReturnSocket( Socket socket, out Boolean isReturned ) =>
            InternalReturnSocket( socket, SocketStateInPool.IsInPool, out isReturned );

        private void InternalReturnSocket(
            Socket socket,
            SocketStateInPool newState,
            out Boolean isReturned )
        {
            //first we need to add socket to the pool in order to it always
            //was in any dictionary and another threads don't create new socket
            m_sockets.AddOrUpdate(
                socket.Id,
                addValueFactory: socketId => socket,
                updateValueFactory: ( socketId, oldSocket ) => socket
            );

            //socket will be not successfully returned to the pool 
            //if it doesn't exist in m_leasedSockets
            isReturned = m_leasedSockets.TryRemove( socket.Id, value: out _ );

            socket.AllowTakeSocket( newState );

#if CONNECTION_POOL_TEST
            if ( isReturned )
            {
                DsLoggerSet.DefaultLogger.LogInfo( logRecord:
                    $"Socket with ID {socket.Id} successfully returned in the pool" );
            }
#endif
        }

        private async ValueTask<(Boolean isTaken, Socket desiredSocket)> TryTakeLeasedSocketAsync(
            EndPoint remoteEndPoint,
            IoBehavior ioBehavior,
            TimeSpan timeWaitToReturnToPool,
            CancellationToken cancellationToken = default
        )
        {
            Boolean takenSocket = m_leasedSockets.TryGetValue( remoteEndPoint, out Socket desiredSocket );

            if ( takenSocket )
            {
                await TakeFromPoolAsync( desiredSocket, ioBehavior, timeWaitToReturnToPool, cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );

                //if another thread wait to take socket, it can do that.
                //Otherwise it can create new socket, because the previous one can be disposed.
                //See ConnectionPool.ConnectedSocketAsync
                AddLeasedSocket( remoteEndPoint, desiredSocket );

                Boolean isTaken = m_sockets.TryRemove( remoteEndPoint, out Socket takenFromPoolSocket );
                if ( isTaken )
                {
                    desiredSocket = takenFromPoolSocket;
                }
            }

            return (takenSocket, desiredSocket);
        }

        private void AddLeasedSocket( EndPoint socketId, Socket socket )
        {
            Boolean isUpdated = false;
            m_leasedSockets.AddOrUpdate( socketId, ( key ) => socket, ( key, previousSocketValue ) =>
               {
                   isUpdated = true;
                   return socket;
               } );

            if ( isUpdated )
            {
                NotifyPoolError( message: $"Taken socket with ID {socket.Id}, which is being used by another thread" );
            }
        }

        private void ReleaseSocketSemaphore()
        {
            try
            {
                m_socketsSemaphore.Release();
            }
            catch ( SemaphoreFullException )
            {
                ;//do nothing
            }
        }

        /// <returns>
        /// It will return <a href="true"/> if socket is returned by another thread during <paramref name="waitTimeout"/>.
        /// It will return <a href="false"/> if socket isn't returned
        /// </returns>
        private ValueTask<Boolean> CanSocketBeTakenFromPoolAsync(
            IoBehavior ioBehavior,
            TimeSpan waitTimeout )
        {
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                return new ValueTask<Boolean>( task: m_socketsSemaphore.WaitAsync( waitTimeout ) );
            }
            else
            {
                Boolean successWait = m_socketsSemaphore.Wait( waitTimeout );
                return m_cachedValueTasks[ successWait ];
            }
        }
    }
}