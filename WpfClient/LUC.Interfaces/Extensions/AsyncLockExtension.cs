using LUC.Interfaces.Enums;

using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.Interfaces.Extensions
{
    public static class AsyncLockExtension
    {
        public static Task<IDisposable> LockAsync( this AsyncLock asyncLock, IoBehavior ioBehavior ) =>
            asyncLock.LockAsync( ioBehavior, CancellationToken.None );

        public static Task<IDisposable> LockAsync( this AsyncLock asyncLock, IoBehavior ioBehavior, CancellationToken cancellationToken )
        {
            Task<IDisposable> lockTask = asyncLock.LockAsync( cancellationToken );

            switch ( ioBehavior )
            {
                case IoBehavior.Asynchronous:
                {
                    break;
                }

                case IoBehavior.Synchronous:
                {
                    lockTask.WaitAndUnwrapException( cancellationToken );
                    break;
                }

                default:
                {
                    throw new ArgumentException( message: $"Has incorrect value: {ioBehavior}", paramName: nameof( ioBehavior ) );
                }
            }

            return lockTask;
        }
            
    }
}
