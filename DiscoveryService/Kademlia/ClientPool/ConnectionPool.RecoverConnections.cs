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

using LUC.DiscoveryServices.Common;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    sealed partial class ConnectionPool
    {
        private CancellationTokenSource m_cancellationRecover;

        public async Task TryRecoverAllConnectionsAsync()
        {
            if ( ( m_sockets.Count > 0 ) || ( m_leasedSockets.Count > 0 ) )
            {
                UpdateRecoveryPars();

                IEnumerable<EndPoint> idsOfRecoveredConnection = null;
                if ( m_leasedSockets.Count > 0 )
                {
                    idsOfRecoveredConnection = IdsOfRecoveredConnectionInLeasedSockets( m_cancellationRecover.Token );
                }

                if ( m_cancellationRecover.IsCancellationRequested )
                {
                    return;
                }

                await RecoverPoolSocketsAsync( idsOfRecoveredConnection, 
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

        private IEnumerable<EndPoint> IdsOfRecoveredConnectionInLeasedSockets( CancellationToken cancellationToken )
        {
            BlockingCollection<EndPoint> socketsWithRecoveredConnection = new BlockingCollection<EndPoint>();

            Task.Factory.StartNew( () =>
            {
                ParallelOptions parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Constants.MAX_THREADS,
                    CancellationToken = cancellationToken
                };

                ConcurrentBag<ConnectionPoolSocket> receivedSockets = new ConcurrentBag<ConnectionPoolSocket>();
                try
                {
                    //recover leased sockets
                    Parallel.ForEach( m_leasedSockets, parallelOptions, ( socket ) =>
                    {
                        Boolean isTaken = false;
                    
                        //socket can be returned to pool before we receive it by another thread and place, so takenSocket can be null
                        isTaken = TryTakeLeasedSocket( socket.Key, out ConnectionPoolSocket takenSocket );
                    
                        if ( isTaken )
                        {
                            //this row should be before the next one to avoid concurrency bugs
                            receivedSockets.Add( takenSocket );
                            BackgroundConnectionResetHelper.AddSocket( takenSocket, cancellationToken );
                    
                            socketsWithRecoveredConnection.Add( socket.Key );
                        }
                    } );
                }
                catch ( OperationCanceledException ex )
                {
                    HandleCancellationException( receivedSockets, ex );
                }
                finally
                {
                    socketsWithRecoveredConnection.CompleteAdding();
                }
            } );

            return socketsWithRecoveredConnection.GetConsumingEnumerable(cancellationToken);
        }

        private void HandleCancellationException(ConcurrentBag<ConnectionPoolSocket> sockets, OperationCanceledException exception)
        {
            ParallelOptions options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Constants.MAX_THREADS
            };

            Parallel.ForEach( sockets, options, ( takenSocket ) =>
            {
                try
                {
                    if ( ( takenSocket.StateInPool != SocketStateInPool.IsInPool ) )
                    {
                        takenSocket.ReturnedToPool();
                    }
                }
                catch(ObjectDisposedException)
                {
                    ;//ignore exception, try return the next one to pool
                }
            } );

            throw exception;
        }

        private async ValueTask RecoverPoolSocketsAsync( IEnumerable<EndPoint> idsOfRecoveredConnection, CancellationToken cancellationToken )
        {
            ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );
            ConcurrentBag<ConnectionPoolSocket> receivedSockets = new ConcurrentBag<ConnectionPoolSocket>();

            //TODO: change to using Parallel.ForEach
            ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>> recoverSockets = new ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>>( socket =>
            {
                //don't change way of the next operations
                m_sockets.TryRemove( socket.Key, out _ );

                socket.Value.StateInPool = SocketStateInPool.TakenFromPool;

                //this row should be before the next one to avoid concurrency bugs(we can add socket to m_leasedSockets,
                //but don't return to pool and another thread will wait while it is returned)
                receivedSockets.Add( socket.Value );
                AddLeasedSocket( socket.Key, socket.Value );

                BackgroundConnectionResetHelper.AddSocket( socket.Value, cancellationToken );

            }, parallelOptions );

            foreach ( KeyValuePair<EndPoint, ConnectionPoolSocket> socket in m_sockets )
            {
                Boolean isConnAlreadyRecovered = idsOfRecoveredConnection != null && idsOfRecoveredConnection.Contains( socket.Key );
                if ( !isConnAlreadyRecovered )
                {
                    await recoverSockets.SendAsync( socket );
                }
            }

            recoverSockets.Complete();

            try
            {
                await recoverSockets.Completion.ConfigureAwait( continueOnCapturedContext: false );
            }
            catch ( OperationCanceledException ex )
            {
                HandleCancellationException( receivedSockets, ex );
            }
        }

        private ExecutionDataflowBlockOptions ParallelOptions( CancellationToken cancellationToken ) =>
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Constants.MAX_THREADS,
                MaxMessagesPerTask = 1
            };

        private async ValueTask<ConnectionPoolSocket> TakenSocketWithRecoveredConnectionAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IOBehavior ioBehavior, ConnectionPoolSocket takenSocket )
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
