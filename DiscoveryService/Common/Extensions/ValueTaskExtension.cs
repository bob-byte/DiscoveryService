using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices.Common.Extensions
{
    static class ValueTaskExtension
    {
        public static async ValueTask WaitAsync( IoBehavior ioBehavior, TimeSpan timeToWait )
        {
            if ( ioBehavior == IoBehavior.Asynchronous )
            {
                await Task.Delay( timeToWait ).ConfigureAwait( continueOnCapturedContext: false );
            }
            else if ( ioBehavior == IoBehavior.Synchronous )
            {
                Thread.Sleep( timeToWait );
            }
            else
            {
                throw new ArgumentException( message: $"{nameof( ioBehavior )} has incorrect value" );
            }
        }
    }
}
