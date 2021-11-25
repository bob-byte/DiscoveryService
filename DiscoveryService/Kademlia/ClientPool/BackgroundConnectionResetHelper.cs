using LUC.DiscoveryServices.Common;
using LUC.Interfaces;
using LUC.Services.Implementation;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Kademlia.ClientPool
{
    static class BackgroundConnectionResetHelper
    {
        static readonly Object s_lock = new Object();
        static readonly SemaphoreSlim s_semaphore = new SemaphoreSlim( initialCount: 1, maxCount: 1 );

        static readonly CancellationTokenSource s_cancellationTokenSource = new CancellationTokenSource();

        static readonly List<Task<Boolean>> s_resetTasks = new List<Task<Boolean>>();
        static Task s_workerTask;

        static BackgroundConnectionResetHelper()
        {
            LoggingService = new LoggingService
            {
                SettingsService = new SettingsService()
            };
        }

        [Import( typeof( ILoggingService ) )]
        internal static ILoggingService LoggingService { get; private set; }

        public static void AddSocket( ConnectionPoolSocket socket, CancellationToken cancellationToken = default )
        {
            SocketAsyncEventArgs disconnetArgs = new SocketAsyncEventArgs();
            Task<Boolean> resetTask = socket.TryRecoverConnectionAsync( returnToPool: true, reuseSocket: false, Constants.DisconnectTimeout, Constants.ConnectTimeout, IOBehavior.Asynchronous, cancellationToken );
            lock ( s_lock )
            {
                s_resetTasks.Add( resetTask );
            }

#if DEBUG
            LoggingService.LogInfo( $"Started Session {socket.Id} reset in background; waiting TaskCount: {s_resetTasks.Count}." );
#endif

            // release only if it is likely to succeed
            if ( s_semaphore.CurrentCount == 0 )
            {
#if DEBUG
                LoggingService.LogInfo( "Releasing semaphore." );
#endif

                try
                {
                    s_semaphore.Release();
                }
                catch ( SemaphoreFullException )
                {
                    // ignore
                }
            }
        }

        public static void Start()
        {
            LoggingService.LogInfo( "Starting BackgroundConnectionResetHelper worker." );

            if ( s_workerTask == null )
            {
                lock ( s_lock )
                {
                    if ( s_workerTask == null )
                    {
                        s_workerTask = Task.Run( async () => await ReturnSocketsAsync() );
                    }
                }
            }
        }

        public static void Stop()
        {
            LoggingService.LogInfo( "Stopping BackgroundConnectionResetHelper worker." );
            s_cancellationTokenSource.Cancel();
            Task workerTask;
            lock ( s_lock )
            {
                workerTask = s_workerTask;
            }

            if ( workerTask != null )
            {
                try
                {
                    workerTask.GetAwaiter().GetResult();
                }
                catch ( OperationCanceledException )
                {
                    ;//do nothing
                }
            }
            LoggingService.LogInfo( "Stopped BackgroundConnectionResetHelper worker." );
        }

        public static async Task ReturnSocketsAsync()
        {
            LoggingService.LogInfo( "Started BackgroundConnectionResetHelper worker." );

            List<Task<Boolean>> localTasks = new List<Task<Boolean>>();

            //keep running until stopped
            while ( !s_cancellationTokenSource.IsCancellationRequested )
            {
                try
                {
#if DEBUG
                    LoggingService.LogInfo( "Waiting for semaphore" );
#endif

                    await s_semaphore.WaitAsync( s_cancellationTokenSource.Token ).ConfigureAwait( continueOnCapturedContext: false );

                    //process all sockets that have started being returned
                    while ( true )
                    {
                        lock ( s_lock )
                        {
                            localTasks.AddRange( s_resetTasks );
                            s_resetTasks.Clear();
                        }

                        if ( localTasks.Count == 0 )
                        {
                            break;
                        }

#if DEBUG
                        LoggingService.LogInfo( $"Found TaskCount {localTasks.Count} task(-s) to process" );
#endif

                        await Task.WhenAll( localTasks ).ConfigureAwait( continueOnCapturedContext: false );
                        localTasks.Clear();
                    }
                }
                catch ( Exception ex ) when ( !( ex is OperationCanceledException canceledException && canceledException.CancellationToken == s_cancellationTokenSource.Token ) )
                {
                    LoggingService.LogError( $"Unhandled exception: {ex}" );
                }
            }
        }
    }
}
