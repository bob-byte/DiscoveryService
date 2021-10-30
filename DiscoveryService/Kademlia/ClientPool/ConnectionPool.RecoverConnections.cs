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
            if ( ( m_sockets.Count > 0 ) || ( m_leasedSockets.Count > 0 ) )
            {
                UpdateRecoveryPars();

                IEnumerable<EndPoint> idsOfRecoveredConnection = null;
                if ( m_leasedSockets.Count > 0 )
                {
                    idsOfRecoveredConnection = IdsOfRecoveredConnectionInLeasedSockets( timeWaitToReturnToPool,
                        m_cancellationRecover.Token );
                }

                if ( m_cancellationRecover.IsCancellationRequested )
                {
                    return;
                }

                await RecoverPoolSocketsAsync( idsOfRecoveredConnection, timeWaitToReturnToPool, 
                        m_cancellationRecover.Token ).ConfigureAwait( false );
            }
        }

        public void TryCancelRecoverConnections()
        {
            lock ( m_cancellationRecover )
            {
                m_cancellationRecover.Cancel();
            }
        }

        private void UpdateRecoveryPars()
        {
            //use lock, because method TryCancelRecoverConnections want to cancel m_cancellationRecover
            lock ( m_cancellationRecover )
            {
                m_cancellationRecover = new CancellationTokenSource();
            }
        }

        private IEnumerable<EndPoint> IdsOfRecoveredConnectionInLeasedSockets( TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken )
        {
            BlockingCollection<EndPoint> socketsWithRecoveredConnection = new BlockingCollection<EndPoint>();

            Task.Factory.StartNew( async () =>
            {
                ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );

                //recover leased sockets
                ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>> recoverLeasedSockets = new ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>>( socket =>
                {
                    //socket can be returned to pool before we receive it by another thread and place, so takenSocket can be null
                    Boolean isTaken = TryTakeLeasedSocket( socket.Key, timeWaitToReturnToPool, out ConnectionPoolSocket takenSocket );

                    if ( isTaken )
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

                socketsWithRecoveredConnection.CompleteAdding();
            } );

            return socketsWithRecoveredConnection.GetConsumingEnumerable();
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

        private ExecutionDataflowBlockOptions ParallelOptions( CancellationToken cancellationToken ) =>
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Constants.MAX_THREADS,
                MaxMessagesPerTask = 1
            };

        private async Task<ConnectionPoolSocket> TakenSocketWithRecoveredConnectionAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, ConnectionPoolSocket takenSocket )
        {
            ConnectionPoolSocket recoveredSocket = takenSocket;
            try
            {
                recoveredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, takenSocket ).
                    ConfigureAwait( false );
            }
            finally
            {
                if ( ( recoveredSocket?.StateInPool != SocketStateInPool.TakenFromPool ) &&
                     ( recoveredSocket.StateInPool != SocketStateInPool.IsFailed ) )
                {
                    recoveredSocket.StateInPool = SocketStateInPool.TakenFromPool;
                }
            }

            return recoveredSocket;
        }
    }
}
