using DiscoveryServices.Common;
using LUC.Interfaces.Constants;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices.Kademlia.ClientPool
{
    static class BackgroundConnectionResetHelper
    {
        private static readonly Object s_lock = new Object();
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        private static readonly CancellationTokenSource s_cancellationTokenSource = new CancellationTokenSource();

        private static readonly List<Task<Boolean>> s_resetTasks = new List<Task<Boolean>>();
        private static Task s_workerTask;

        public static void AddSocket(ConnectionPool.Socket socket, CancellationToken cancellationToken = default)
        {
            try
            {
                ;//do nothing
            }
            finally
            {
                Task<Boolean> resetTask = socket.TryRecoverConnectionAsync(returnToPool: true, reuseSocket: false, DsConstants.DisconnectTimeout, DsConstants.ConnectTimeout, IoBehavior.Asynchronous, cancellationToken);
                lock (s_lock)
                {
                    s_resetTasks.Add(resetTask);
                }

#if DEBUG
                DsLoggerSet.DefaultLogger.LogInfo( $"Started Session {socket.Id} reset in background; waiting TaskCount: {s_resetTasks.Count}." );
#endif

                // release only if it is likely to succeed
                if (s_semaphore.CurrentCount == 0)
                {
#if DEBUG
                    DsLoggerSet.DefaultLogger.LogInfo( "Releasing semaphore." );
#endif

                    try
                    {
                        s_semaphore.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        // ignore
                    }
                }
            }
        }

        public static void Start()
        {
            DsLoggerSet.DefaultLogger.LogInfo( "Starting BackgroundConnectionResetHelper worker." );

            if ( s_workerTask == null )
            {
                lock ( s_lock )
                {
                    if ( s_workerTask == null )
                    {
                        s_workerTask = Task.Run(async () => await ReturnSocketsAsync());
                    }
                }
            }
        }

        public static async Task StopAsync()
        {
            DsLoggerSet.DefaultLogger.LogInfo( "Stopping BackgroundConnectionResetHelper worker." );
            
            s_cancellationTokenSource.Cancel();

            Task workerTask;
            lock ( s_lock )
            {
                workerTask = s_workerTask;
            }

            if (workerTask != null)
            {
                try
                {
                    await workerTask.ConfigureAwait(continueOnCapturedContext: false);
                }
                catch ( OperationCanceledException )
                {
                    ;//do nothing
                }
            }

            DsLoggerSet.DefaultLogger.LogInfo( "Stopped BackgroundConnectionResetHelper worker." );
        }

        private static async Task ReturnSocketsAsync()
        {
            DsLoggerSet.DefaultLogger.LogInfo( "Started BackgroundConnectionResetHelper worker." );

            var localTasks = new List<Task<Boolean>>();

            //keep running until stopped
            while ( !s_cancellationTokenSource.IsCancellationRequested )
            {
                try
                {
#if DEBUG
                    DsLoggerSet.DefaultLogger.LogInfo( "Waiting for semaphore" );
#endif

                    await s_semaphore.WaitAsync(s_cancellationTokenSource.Token).ConfigureAwait(continueOnCapturedContext: false);

                    //process all sockets that have started being returned
                    while ( true )
                    {
                        lock ( s_lock )
                        {
                            localTasks.AddRange(s_resetTasks);
                            s_resetTasks.Clear();
                        }

                        if ( localTasks.Count == 0 )
                        {
                            break;
                        }

#if DEBUG
                        DsLoggerSet.DefaultLogger.LogInfo( $"Found TaskCount {localTasks.Count} task(-s) to process" );
#endif

                        await Task.WhenAll(localTasks).ConfigureAwait(continueOnCapturedContext: false);
                        localTasks.Clear();
                    }
                }
                catch ( Exception ex ) when ( !( ex is OperationCanceledException canceledException && canceledException.CancellationToken == s_cancellationTokenSource.Token ) )
                {
                    DsLoggerSet.DefaultLogger.LogInfo( $"Unhandled exception: {ex}" );
                }
            }
        }
    }
}
