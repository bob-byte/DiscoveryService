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
        private readonly SemaphoreSlim m_recoverSocketsSemaphore;
        private readonly TimeSpan m_maxTimeWaitEndPreviousRecovering;
        private CancellationTokenSource m_cancellationRecover;

        public async Task TryRecoverAllConnectionsAsync( TimeSpan timeWaitToReturnToPool )
        {
            if((m_sockets.Count > 0) || (m_leasedSockets.Count > 0))
            {
                await m_recoverSocketsSemaphore.WaitAsync( m_maxTimeWaitEndPreviousRecovering ).
                    ConfigureAwait( continueOnCapturedContext: false );

                lock ( m_cancellationRecover )
                {
                    m_cancellationRecover = new CancellationTokenSource();
                    m_lastRecoveryTimeInTicks = unchecked((UInt32)Environment.TickCount);
                }

                try
                {
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
                finally
                {
                    m_recoverSocketsSemaphore.Release();
                }
            }
        }

        public void TryCancelRecoverConnections()
        {
            if(IsNowRecovering())
            {
                lock ( m_cancellationRecover )
                {
                    m_cancellationRecover.Cancel();
                }
            }
        }

        private async Task<EndPoint[]> IdsOfRecoveredConnectionInLeasedSocketsAsync( TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken )
        {
            ConcurrentBag<EndPoint> socketsWithRecoveredConnection = new ConcurrentBag<EndPoint>();
            var parallelOptions = ParallelOptions(cancellationToken);

            //recover leased sockets
            ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>> recoverLeasedSockets = new ActionBlock<KeyValuePair<EndPoint, ConnectionPoolSocket>>( async socket =>
            {
                //it is not matter whether take async or synchronously
                _ = await TakeLeasedSocket( socket.Key, IOBehavior.Synchronous, timeWaitToReturnToPool ).
                    ConfigureAwait( continueOnCapturedContext: false );

                BackgroundConnectionResetHelper.AddSocket( socket.Value );

                socketsWithRecoveredConnection.Add( socket.Key );
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
                _ = await IsSocketReturnedToPoolAsync( IOBehavior.Asynchronous, m_socketSemaphore, 
                    timeWaitToReturnToPool ).ConfigureAwait(continueOnCapturedContext: false);
                m_sockets.TryRemove( socket.Key, out _ );

                socket.Value.IsInPool = false;
                m_leasedSockets.TryAdd( socket.Key, socket.Value );

                BackgroundConnectionResetHelper.AddSocket( socket.Value );
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

        private Boolean IsNowRecovering() =>
            m_recoverSocketsSemaphore.CurrentCount == 0;
    }
}
