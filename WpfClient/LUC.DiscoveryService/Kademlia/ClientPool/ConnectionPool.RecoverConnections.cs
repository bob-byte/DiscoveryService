using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using LUC.DiscoveryServices.Common;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    partial class ConnectionPool
    {
        private CancellationTokenSource m_cancellationRecover;

        public async Task TryRecoverAllConnectionsAsync(TimeSpan timeWaitToReturnToPool)
        {
            if (ConnectionSettings.ConnectionBackgroundReset)
            {
                if ((m_sockets.Count > 0) || (m_leasedSockets.Count > 0))
                {
                    UpdateRecoveryPars();

                    //ID is socket ID, so remote endpoint
                    IEnumerable<EndPoint> idsOfRecoveredConnection = null;
                    if (m_leasedSockets.Count > 0)
                    {
                        idsOfRecoveredConnection = await IdsOfRecoveredConnectionsInLeasedSockets(timeWaitToReturnToPool, m_cancellationRecover.Token).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    m_cancellationRecover.Token.ThrowIfCancellationRequested();

                    await RecoverPoolSocketsAsync(timeWaitToReturnToPool, idsOfRecoveredConnection,
                            m_cancellationRecover.Token).ConfigureAwait(false);
                }
            }
            else
            {
                throw new InvalidOperationException(message: $"Connection reset is not supporting");
            }
        }

        public void CancelRecoverConnections()
        {
            try
            {
                m_cancellationRecover.Cancel();
            }
            catch(ObjectDisposedException)
            {
                ;//do nothing
            }
        }

        private void UpdateRecoveryPars()
        {
            m_cancellationRecover.Dispose();
            Interlocked.Exchange(ref m_cancellationRecover, value: new CancellationTokenSource());
        }

        private async ValueTask<ConcurrentBag<EndPoint>> IdsOfRecoveredConnectionsInLeasedSockets( TimeSpan timeWaitToReturnToPool, CancellationToken cancellationToken = default )
        {
            ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );
            var socketsWithRecoveredConnections = new ConcurrentBag<EndPoint>();

            var recoverLeasedSockets = new ActionBlock<KeyValuePair<EndPoint, Socket>>( 
                socket =>
                {
                    //TODO use cancellationToken argument
                    (Boolean isTaken, Socket takenSocket) = TryTakeLeasedSocketAsync( 
                        socket.Key, 
                        IoBehavior.Synchronous, 
                        timeWaitToReturnToPool 
                    ).GetAwaiter().GetResult();
                
                    if(isTaken)
                    {
                        BackgroundConnectionResetHelper.AddSocket( takenSocket, cancellationToken );
                
                        socketsWithRecoveredConnections.Add( socket.Key );
                    }
                    
                }, 
                parallelOptions 
            );

            Parallel.ForEach( m_leasedSockets, ( leasedSocket ) => recoverLeasedSockets.Post( leasedSocket ) );

            //Signals that we will not post more leasedSocket. 
            //recoverSockets.Completion will never be completed without this call
            recoverLeasedSockets.Complete();

            try
            {
                //await completion of adding all leased
                //sockets to BackgroundConnectionResetHelper
                await recoverLeasedSockets.Completion.ConfigureAwait( false );
            }
            catch ( OperationCanceledException )
            {
                ;//do nothing
            }

            return socketsWithRecoveredConnections;
        }

        private async ValueTask RecoverPoolSocketsAsync( TimeSpan timeWaitToReturnToPool, IEnumerable<EndPoint> idsOfRecoveredConnection, CancellationToken cancellationToken )
        {
            ExecutionDataflowBlockOptions parallelOptions = ParallelOptions( cancellationToken );

            var recoverSockets = new ActionBlock<KeyValuePair<EndPoint, Socket>>( socket =>
             {
                 m_sockets.TryRemove( socket.Key, value: out _ );
                 TakeFromPoolAsync( socket.Value, IoBehavior.Synchronous, timeWaitToReturnToPool ).GetAwaiter().GetResult();

                 AddLeasedSocket( socket.Key, socket.Value );
                 BackgroundConnectionResetHelper.AddSocket( socket.Value, cancellationToken );

             }, parallelOptions );

            foreach ( KeyValuePair<EndPoint, Socket> socket in m_sockets )
            {
                Boolean isConnAlreadyRecovered = ( idsOfRecoveredConnection != null ) && idsOfRecoveredConnection.Contains( socket.Key );

                if ( !isConnAlreadyRecovered )
                {
                    await recoverSockets.SendAsync( socket ).ConfigureAwait( continueOnCapturedContext: false );
                }
            }

            //Signals that we will not post more sockets. 
            //recoverSockets.Completion will never be completed without this call
            recoverSockets.Complete();

            try
            {
                //await completion of adding to 
                //BackgroundConnectionResetHelper all sockets
                await recoverSockets.Completion.ConfigureAwait( false );
            }
            catch ( OperationCanceledException )
            {
                ;//do nothing
            }
        }

        private ExecutionDataflowBlockOptions ParallelOptions( CancellationToken cancellationToken ) =>
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = DsConstants.MAX_THREADS,
                MaxMessagesPerTask = 1
            };

        private async ValueTask<Socket> TakenSocketWithRecoveredConnectionAsync( EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IoBehavior ioBehavior, Socket takenSocket )
        {
            Socket recoveredSocket = takenSocket;
            Exception exception = null;

            try
            {
                recoveredSocket = await ConnectedSocketAsync( remoteEndPoint, timeoutToConnect, ioBehavior, takenSocket, createNewSocketIfDisposed: true, handleRemoteException: false ).
                    ConfigureAwait( false );
            }
            catch ( SocketException ex )
            {
                exception = ex;
                throw;
            }
            catch ( InvalidOperationException ex )
            {
                exception = ex;
                throw;
            }
            catch ( TimeoutException ex )
            {
                exception = ex;
                throw;
            }
            finally
            {
                if ( exception != null )
                {
                    Boolean wasInPool = m_leasedSockets.ContainsKey( remoteEndPoint );
                    if ( wasInPool )
                    {
                        recoveredSocket.DisposeUnmanagedResources();
                        InternalReturnSocket( recoveredSocket, newState: SocketStateInPool.IsFailed, isReturned: out _ );
                    }

                    ReleaseSocketSemaphore();
                }
            }

            return recoveredSocket;
        }
    }
}
