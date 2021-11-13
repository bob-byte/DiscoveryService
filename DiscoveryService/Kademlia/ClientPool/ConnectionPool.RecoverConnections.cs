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
            //use lock, because method TryCancelRecoverConnections cancels m_cancellationRecover
            lock ( m_cancellationRecover )
            {
                m_cancellationRecover = new CancellationTokenSource();
            }
        }

        private IEnumerable<EndPoint> IdsOfRecoveredConnectionInLeasedSockets( TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken )
        {
            BlockingCollection<EndPoint> socketsWithRecoveredConnection = new BlockingCollection<EndPoint>();

            Task.Factory.StartNew( () =>
            {
                ParallelOptions parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Constants.MAX_THREADS,
                    CancellationToken = cancellationToken
                };

                //recover leased sockets
                Parallel.ForEach( m_leasedSockets, parallelOptions, ( socket ) =>
                 {
                     Boolean isTaken = false;
                     try
                     {
                         //socket can be returned to pool before we receive it by another thread and place, so takenSocket can be null
                         isTaken = TryTakeLeasedSocket( socket.Key, out ConnectionPoolSocket takenSocket );

                         if ( isTaken )
                         {
                             AddLeasedSocket( socket.Key, socket.Value );
                             BackgroundConnectionResetHelper.AddSocket( takenSocket, cancellationToken );

                             socketsWithRecoveredConnection.Add( socket.Key );
                         }
                     }
                     catch ( OperationCanceledException )
                     {
                         if ( ( isTaken ) && ( socket.Value.StateInPool != SocketStateInPool.IsInPool ) )
                         {
                             socket.Value.ReturnedToPool();
                         }

                         ReleaseSocketSemaphore();

                         throw;
                     }
                 } );

                socketsWithRecoveredConnection.CompleteAdding();
            } );

            return socketsWithRecoveredConnection.GetConsumingEnumerable();
        }

        

        private async Task RecoverPoolSocketsAsync(IEnumerable<EndPoint> idsOfRecoveredConnection, TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken)
        {
            ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );

            //here we call async method, so we cannot use Parallel.ForEach(in this case it will be ended before all iterations)
            ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>> recoverSockets = new ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>>( async socket =>
            {
                Boolean isAddedToLeasedSocket = false;
                //wait while any socket returns to pool
                try
                {
                    _ = await CanSocketBeTakenFromPoolAsync( IOBehavior.Asynchronous, m_socketSemaphore,
                    timeWaitToReturnToPool ).ConfigureAwait( continueOnCapturedContext: false );

                    socket.Value.StateInPool = SocketStateInPool.TakenFromPool;
                    AddLeasedSocket( socket.Key, socket.Value );
                    isAddedToLeasedSocket = true;

                    m_sockets.TryRemove( socket.Key, out _ );

                    BackgroundConnectionResetHelper.AddSocket( socket.Value, cancellationToken );
                }
                catch(OperationCanceledException)
                {
                    if ( ( isAddedToLeasedSocket ) && ( socket.Value.StateInPool != SocketStateInPool.IsInPool ) )
                    {
                        socket.Value.ReturnedToPool();
                    }

                    ReleaseSocketSemaphore();

                    throw;
                }
            }, parallelOptions );

            foreach ( KeyValuePair<EndPoint, ConnectionPoolSocket> socket in m_sockets )
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
