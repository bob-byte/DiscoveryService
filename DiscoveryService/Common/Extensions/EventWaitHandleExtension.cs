using System;
using System.Threading;

namespace DiscoveryServices.Common.Extensions
{
    static class EventWaitHandleExtension
    {
        //TODO add parameter IoBehavior and comment out code
        public static void SafeSet( this EventWaitHandle eventWait, out Boolean isSet )
        {
            //for case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
            try
            {
                ;//do nothing
            }
            finally
            {
                isSet = false;

                if ( !eventWait.SafeWaitHandle.IsClosed )
                {
                    isSet = eventWait.Set();

                    //try set while core has some issues
                    //if ( !isSet )
                    //{
                    //    Thread.Sleep( TimeSpan.FromSeconds( value: 0.5 ) );

                    //    Int32 maxRetries = 5;
                    //    for ( Int32 numRetry = 1; ( numRetry < maxRetries ) && !isSet; numRetry++ )
                    //    {
                    //        isSet = eventWait.Set();

                    //        if ( !isSet )
                    //        {
                    //            Thread.Sleep( TimeSpan.FromSeconds( 0.5 ) );
                    //        }
                    //    }
                    //}
                }
            }
        }
    }
}
