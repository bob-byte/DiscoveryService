using Nito.AsyncEx;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LUC.Interfaces
{
    public interface IBackgroundSynchronizer
    {
        Boolean IsTickSyncFromServerStarted { get; }

        Boolean IsSyncToServerNow { get; }

        CancellationTokenSource SourceToCancelSyncToServer { get; }

        DispatcherTimer TimerSyncFromServer { get; }

        AsyncLock LockNotifyAndCheckSyncStart { get; }

        Task RunAsync();

        void RunPeriodicSyncFromServer( Boolean whetherRunImmediatelySyncProcess = true );

        /// <summary>
        /// Stop synchronization to and from the server
        /// </summary>
        void StopAllSync();

        void StopSyncFromServer();

        Task TrySyncAllFromServerAsync();
    }
}
