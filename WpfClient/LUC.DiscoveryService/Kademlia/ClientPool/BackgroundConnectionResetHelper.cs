using LUC.DiscoveryServices.Common;
using LUC.Interfaces.Constants;
using LUC.Interfaces.Enums;
using LUC.Interfaces.Helpers;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    static class BackgroundConnectionResetHelper
    {
        //locks s_workerTask initialization
        private static readonly Object s_mutex = new Object();
        private static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim( initialCount: 1, maxCount: 1 );

        private static readonly CancellationTokenSource s_cancellationTokenSource = new CancellationTokenSource();

        private static readonly List<Task<Boolean>> s_resetTasks = new List<Task<Boolean>>();
        private static Task s_workerTask;

        public static void AddSocket( ConnectionPool.Socket socket, CancellationToken cancellationToken = default )
        {
            Task<Boolean> resetTask = socket.TryRecoverConnectionAsync( returnToPool: true, DsConstants.ConnectTimeout, IoBehavior.Asynchronous, cancellationToken );
            lock ( s_mutex )
            {
                s_resetTasks.Add( resetTask );
            }

            DsLoggerSet.DefaultLogger.LogInfo( $"Started Session {socket.Id} reset in background; TaskCount: {s_resetTasks.Count}." );

            // release only if it is likely to succeed
            if ( s_semaphore.CurrentCount == 0 )
            {
                try
                {
                    s_semaphore.Release();
                }
                catch ( SemaphoreFullException )
                {
                    ;// ignore
                }
            }
        }

        public static void Start()
        {
            DsLoggerSet.DefaultLogger.LogInfo( "Starting BackgroundConnectionResetHelper worker." );

            SingletonInitializer.ThreadSafeInit( value: () => Task.Run( ReturnSocketsAsync ), s_mutex, ref s_workerTask );
        }

        public static async Task StopAsync()
        {
            DsLoggerSet.DefaultLogger.LogInfo( "Stopping BackgroundConnectionResetHelper worker." );

            s_cancellationTokenSource.Cancel();

            Task workerTask;
            lock ( s_mutex )
            {
                workerTask = s_workerTask;
            }

            if ( workerTask != null )
            {
                try
                {
                    await workerTask.ConfigureAwait( continueOnCapturedContext: false );
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
            DsLoggerSet.DefaultLogger.LogInfo( logRecord: "Started BackgroundConnectionResetHelper worker." );

            var localTasks = new List<Task<Boolean>>();

            //keep running until stopped
            while ( !s_cancellationTokenSource.IsCancellationRequested )
            {
                try
                {
                    await s_semaphore.WaitAsync( s_cancellationTokenSource.Token ).ConfigureAwait( continueOnCapturedContext: false );

                    //process all sockets that have started being returned
                    while ( true )
                    {
                        lock ( s_mutex )
                        {
                            localTasks.AddRange( s_resetTasks );
                            s_resetTasks.Clear();
                        }

                        if ( localTasks.Count == 0 )
                        {
                            break;
                        }

                        DsLoggerSet.DefaultLogger.LogInfo( $"Found {localTasks.Count} task(-s) to process connection recovering" );
                        await Task.WhenAll( localTasks ).ConfigureAwait( continueOnCapturedContext: false );
                        DsLoggerSet.DefaultLogger.LogInfo( $"Successfully processed {localTasks.Count} task(-s) of connection recovering" );

                        localTasks.Clear();
                    }
                }
                catch ( Exception ex ) when ( !( ex is OperationCanceledException canceledException && canceledException.CancellationToken == s_cancellationTokenSource.Token ) )
                {
                    DsLoggerSet.DefaultLogger.LogCriticalError( message: "Unhandled exception during connection recovering", ex );
                }
            }
        }
    }
}
