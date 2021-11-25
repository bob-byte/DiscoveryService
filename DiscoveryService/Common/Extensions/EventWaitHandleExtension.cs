using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common.Extensions
{
    static class EventWaitHandleExtension
    {
        public static void SafeSet(this EventWaitHandle eventWait, out Boolean isSet)
        {
            //for case if ThreadAbortException is thrown (the whole finally block will be finished first, as it is a critical section). 
            try
            {
                ;//do nothing
            }
            finally
            {
                if ( !eventWait.SafeWaitHandle.IsClosed )
                {
                    isSet = eventWait.Set();
                }
                else
                {
                    isSet = false;
                }
            }
        }
    }
}
