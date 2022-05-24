using System;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.Interfaces.Helpers
{
    public static partial class AsyncHelper
    {
        /// <summary>
        /// Execute's an async Task<T> method which has a void return value synchronously
        /// </summary>
        /// <param name="task">Task<T> method to execute</param>
        public static void RunSync( Func<Task> task )
        {
            SynchronizationContext oldContext = SynchronizationContext.Current;

            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext( synch );

            synch.Post( async _ =>
            {
                try
                {
                    await task();
                }
                catch ( Exception e )
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null );
            synch.BeginMessageLoop();

            SynchronizationContext.SetSynchronizationContext( oldContext );
        }

        /// <summary>
        /// Execute's an async Task<T> method which has a T return type synchronously
        /// </summary>
        /// <typeparamref name="T">
        /// Return Type
        /// </typeparamref>
        /// <paramref name="task">Task<T> method to execute</paramref>
        public static T RunSync<T>( Func<Task<T>> task )
        {
            SynchronizationContext oldContext = SynchronizationContext.Current;

            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext( synch );

            T ret = default;
            synch.Post( async _ =>
            {
                try
                {
                    ret = await task();
                }
                catch ( Exception e )
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null );
            synch.BeginMessageLoop();
            SynchronizationContext.SetSynchronizationContext( oldContext );
            return ret;
        }
    }
}
