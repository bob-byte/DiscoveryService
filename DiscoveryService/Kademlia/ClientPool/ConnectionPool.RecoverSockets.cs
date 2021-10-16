using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using LUC.DiscoveryService.Common;

namespace LUC.DiscoveryService.Kademlia.ClientPool
{
    sealed partial class ConnectionPool
    {
        private CancellationTokenSource m_cancellationRecover;

        public async Task TryRecoverAllConnectionsAsync( TimeSpan timeWaitToReturnToPool )
        {
            if((m_sockets.Count > 0) || (m_leasedSockets.Count > 0))
            {
                UpdateRecoveryPars();

                EndPoint[] idsOfRecoveredConnection = null;
                if ( m_leasedSockets.Count > 0 )
                {
                    idsOfRecoveredConnection = await IdsOfRecoveredConnectionInLeasedSocketsAsync( timeWaitToReturnToPool,
                        m_cancellationRecover.Token ).ConfigureAwait( continueOnCapturedContext: false );
                }

                if ( m_cancellationRecover.IsCancellationRequested )
                {
                    return;
                }

                await RecoverPoolSocketsAsync( idsOfRecoveredConnection, timeWaitToReturnToPool, 
                        m_cancellationRecover.Token ).ConfigureAwait( false );
            }
        }

        private void UpdateRecoveryPars()
        {
            //use lock, because method TryCancelRecoverConnections want to cancel m_cancellationRecover
            lock ( m_cancellationRecover )
            {
                m_cancellationRecover = new CancellationTokenSource();
                m_lastRecoveryTimeInTicks = unchecked((UInt32)Environment.TickCount);
            }
        }

        public void TryCancelRecoverConnections()
        {
            lock ( m_cancellationRecover )
            {
                m_cancellationRecover.Cancel();
            }
        }

        private async Task<EndPoint[]> IdsOfRecoveredConnectionInLeasedSocketsAsync( TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken )
        {
            ConcurrentBag<EndPoint> socketsWithRecoveredConnection = new ConcurrentBag<EndPoint>();
            var parallelOptions = ParallelOptions(cancellationToken);

            //recover leased sockets
            ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>> recoverLeasedSockets = new ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>>( socket =>
            {
                //socket can be returned to pool before we receive it by another thread and place, so takenSocket can be null
                Boolean isTaken = TryTakeLeasedSocket( socket.Key, timeWaitToReturnToPool, out ConnectionPoolSocket takenSocket );

                if(isTaken)
                {
                    BackgroundConnectionResetHelper.AddSocket( takenSocket, cancellationToken );

                    socketsWithRecoveredConnection.Add( socket.Key );
                }
            }, parallelOptions );

            foreach ( var takenSocket in m_leasedSockets )
            {
                recoverLeasedSockets.Post( takenSocket );
            }

            recoverLeasedSockets.Complete();
            await recoverLeasedSockets.Completion.ConfigureAwait( continueOnCapturedContext: false );

            return socketsWithRecoveredConnection.ToArray();
        }

        private async Task RecoverPoolSocketsAsync(IEnumerable<EndPoint> idsOfRecoveredConnection, TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken)
        {
            var parallelOptions = ParallelOptions( cancellationToken );

            ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>> recoverSockets = new ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>>( async socket =>
            {
                //wait while any socket returns to pool
                _ = await CanSocketBeTakenFromPoolAsync( IOBehavior.Asynchronous, m_socketSemaphore, 
                    timeWaitToReturnToPool ).ConfigureAwait(continueOnCapturedContext: false);

                socket.Value.StateInPool = SocketStateInPool.TakenFromPool;
                m_leasedSockets.TryAdd( socket.Key, socket.Value );

                m_sockets.TryRemove( socket.Key, out _ );

                BackgroundConnectionResetHelper.AddSocket( socket.Value, cancellationToken );
            } );

            foreach ( var socket in m_sockets )
            {
                Boolean isConnAlreadyRecovered = idsOfRecoveredConnection != null && idsOfRecoveredConnection.Contains( socket.Key );
                if ( !isConnAlreadyRecovered )
                {
                    recoverSockets.Post( socket );
                }
            }

            recoverSockets.Complete();
            await recoverSockets.Completion.ConfigureAwait( continueOnCapturedContext: false );
        }

        private async Task ShallowRecoverPoolSocketsAsync( TimeSpan timeoutToConnect, CancellationToken cancellationToken )
        {
            List<ConnectionPoolSocket> recoveredSockets = new List<ConnectionPoolSocket>();

            lock ( m_lockLastRecoveryTime )
            {
                m_lastRecoveryTimeInTicks = unchecked((UInt32)Environment.TickCount);
            }

            var parallelOptions = ParallelOptions( cancellationToken );
            ActionBlock<ConnectionPoolSocket> recoverSockets = new ActionBlock<ConnectionPoolSocket>( async ( socket ) =>
            {
                try
                {
                    ConnectionPoolSocket restoredSocket = await ConnectedSocketAsync(
                        socket.Id,
                        timeoutToConnect,
                        IOBehavior.Synchronous, //now we don't have any reason do it async
                        socket, 
                        cancellationToken
                    ).ConfigureAwait( false );

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
            }, parallelOptions );

            lock(m_sockets)
            {
                ConnectionPoolSocket[] socketsInPool = m_sockets.Values.ToArray();
                for ( Int32 numSocket = 0; numSocket < socketsInPool.Length; numSocket++ )
                {
                    ConnectionPoolSocket socket = socketsInPool[ numSocket ];

                    m_sockets.TryRemove( socket.Id, out _ );
                    socket.StateInPool = SocketStateInPool.TakenFromPool;
                    m_leasedSockets.TryAdd( socket.Id, socket );

                    recoverSockets.Post( socket );
                }
            }

            recoverSockets.Complete();

            await recoverSockets.Completion;

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

        private ExecutionDataflowBlockOptions ParallelOptions( CancellationToken cancellationToken ) =>
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Constants.MAX_THREADS,
                MaxMessagesPerTask = 1
            };
    }
}
