using LUC.Interfaces.Enums;

using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.Interfaces.Extensions
{
    public static class AsyncAutoResetEventExtension
    {
        public static async Task<Boolean> WaitAsync( this AsyncAutoResetEvent autoResetEvent, TimeSpan timeWait, params CancellationToken[] tokens ) =>
            await autoResetEvent.WaitAsync( timeWait, IoBehavior.Asynchronous, tokens ).ConfigureAwait( continueOnCapturedContext: false );

        public static async Task<Boolean> WaitAsync( this AsyncAutoResetEvent autoResetEvent, TimeSpan timeWait, IoBehavior ioBehavior, params CancellationToken[] tokens )
        {
            Boolean isSetInTimeByAnotherThread;

            if ( ( tokens == null ) || ( tokens.Length == 0 ) )
            {
                //tokens[0] = default(CancellationToken)
                tokens = new CancellationToken[ 1 ];
            }

            var cancelWaitSource = CancellationTokenSource.CreateLinkedTokenSource( tokens );

            //plus TimeSpan.FromMilliseconds( value: 100 ) because time already may run out in WaitAsync or Wait
            cancelWaitSource.CancelAfter( timeWait + TimeSpan.FromMilliseconds( value: 100 ) );

            try
            {
                if ( ioBehavior == IoBehavior.Asynchronous )
                {
                    await autoResetEvent.WaitAsync( cancelWaitSource.Token ).ConfigureAwait( continueOnCapturedContext: false );
                }
                else if ( ioBehavior == IoBehavior.Synchronous )
                {
                    autoResetEvent.Wait( cancelWaitSource.Token );
                }
                else
                {
                    throw new ArgumentException( "Incorrect value", nameof( ioBehavior ) );
                }

                isSetInTimeByAnotherThread = true;
            }
            catch ( OperationCanceledException )
            {
                isSetInTimeByAnotherThread = false;
            }
            finally
            {
                cancelWaitSource.Dispose();
            }

            return isSetInTimeByAnotherThread;
        }

        public static Boolean Wait( this AsyncAutoResetEvent autoResetEvent, TimeSpan timeWait, params CancellationToken[] tokens ) =>
            autoResetEvent.WaitAsync( timeWait, IoBehavior.Synchronous, tokens ).WaitAndUnwrapException();
    }
}
