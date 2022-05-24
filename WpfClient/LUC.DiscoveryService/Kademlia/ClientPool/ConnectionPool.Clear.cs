using LUC.DiscoveryServices.Common;

using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    partial class ConnectionPool
    {
        private readonly SemaphoreSlim m_cleanSemaphore;

        public async Task ClearPoolAsync( IoBehavior ioBehavior, Boolean respectMinPoolSize, CancellationToken cancellationToken )
        {
            DsLoggerSet.DefaultLogger.LogInfo( $"Pool clearing connection pool" );

            try
            {
                // synchronize access to this method as only one clean routine should be run at a time
                if ( ioBehavior == IoBehavior.Asynchronous )
                {
                    await m_cleanSemaphore.WaitAsync( cancellationToken ).ConfigureAwait( continueOnCapturedContext: false );
                }
                else if ( ioBehavior == IoBehavior.Synchronous )
                {
                    m_cleanSemaphore.Wait( cancellationToken );
                }
                else
                {
                    throw new ArgumentException( message: "Incorrect value", paramName: nameof( ioBehavior ) );
                }

                CleanSockets( respectMinPoolSize, m_sockets );
                CleanSockets( respectMinPoolSize, m_leasedSockets );
            }
            finally
            {
                try
                {
                    m_cleanSemaphore.Release();
                }
                catch ( SemaphoreFullException )
                {
                    ;//do nothing
                }

                Task taskStopRecoveringConnections = BackgroundConnectionResetHelper.StopAsync();
                if ( ioBehavior == IoBehavior.Asynchronous )
                {
                    await taskStopRecoveringConnections.ConfigureAwait( false );
                }
                else if ( ioBehavior == IoBehavior.Synchronous )
                {
                    AsyncContext.Run( async () => await taskStopRecoveringConnections );
                }
            }
        }

        private void CleanSockets( Boolean respectMinPoolSize, ConcurrentDictionary<EndPoint, Socket> sockets )
        {
            Boolean canAnySocketBeRemoved = sockets.Count > 0;
            while ( canAnySocketBeRemoved )
            {
                CleanOneSocket( respectMinPoolSize, sockets, out canAnySocketBeRemoved );
            }
        }

        private void CleanOneSocket( Boolean respectMinPoolSize, ConcurrentDictionary<EndPoint, Socket> sockets, out Boolean removedAnySocket )
        {
            // if respectMinPoolSize is true, return if (leased sessions + waiting sessions <= minPoolSize)
            if ( respectMinPoolSize )
            {
                if ( ConnectionSettings.MaxCountSocketInUse - m_socketsSemaphore.CurrentCount + m_sockets.Count <= ConnectionSettings.MinimumPoolSize )
                {
                    removedAnySocket = false;
                    return;
                }
            }

            KeyValuePair<EndPoint, Socket> waitingSocket = sockets.FirstOrDefault();

            try
            {
                removedAnySocket = !waitingSocket.Equals( default( KeyValuePair<EndPoint, Socket> ) );
            }
            catch ( ObjectDisposedException )
            {
                removedAnySocket = true;
            }

            if ( removedAnySocket )
            {
                sockets.TryRemove( waitingSocket.Key, value: out _ );

                if ( waitingSocket.Value.StateInPool == SocketStateInPool.TakenFromPool )
                {
                    ReleaseSocketSemaphore();
                }

                waitingSocket.Value.DisposeUnmanagedResourcesAndSetIsFailed();
            }
        }
    }
}
