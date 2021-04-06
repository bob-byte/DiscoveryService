using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices.Extensions
{
    static class TaskExtension
    {
        public static Boolean WhetherToContinueTask(this Task task, CancellationToken token)
        {
            if(task != null)
            {
                var isThisTaskCanceled = task.Status == TaskStatus.Canceled;
                var hasThisTaskException = task.Status == TaskStatus.Faulted;

                return ((!isThisTaskCanceled) && (!hasThisTaskException) && !token.IsCancellationRequested);
            }
            else
            {
                return false;
            }
        }
    }
}
