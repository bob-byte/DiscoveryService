using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;

namespace LUC.Services.Implementation.Helpers
{
    partial class DispatcherWinFormsCompatAdapter : ISynchronizeInvoke
    {
        private sealed class DispatcherAsyncResultAdapter : IAsyncResult
        {
            public DispatcherAsyncResultAdapter( DispatcherOperation dispatcherOperation, Object operationState )
                : this( dispatcherOperation )
            {
                AsyncState = operationState;
            }

            public DispatcherAsyncResultAdapter( DispatcherOperation dispatcherOperation )
            {
                DispatcherOperation = dispatcherOperation;
            }

            public DispatcherOperation DispatcherOperation { get; }

            public Object AsyncState { get; }

            public WaitHandle AsyncWaitHandle => null;

            public Boolean CompletedSynchronously => false;

            public Boolean IsCompleted => DispatcherOperation.Status == DispatcherOperationStatus.Completed;

            public void WaitOperationCompletion() =>
                DispatcherOperation.Wait();
        }
    }
}
