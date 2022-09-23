//#define TEST_CONCURRENCY

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    public static class ConcurrencyErrorForcer
    {
        private const Int32 MAX_SLEEP_TIME_IN_MS = 2000; //2 seconds

        //ThreadLocal<T> has destructor
        private static readonly ThreadLocal<Random> s_threadRandom;

        private static readonly Func<Task>[] s_forcingConcurrencyErrorMethods;

        static ConcurrencyErrorForcer()
        {
            s_threadRandom = new ThreadLocal<Random>( valueFactory: () => new Random() );

            s_forcingConcurrencyErrorMethods = new Func<Task>[5]
            {
                () =>
                {
                    Int32 rndSleepTime = RndSleepTime();
                    Thread.Sleep( rndSleepTime );

                    return Task.CompletedTask;
                },
                () =>
                {
                    Int32 rndSleepTime = RndSleepTime();
                    return Task.Delay( rndSleepTime );
                },

                () =>
                {
                    Thread.Yield();
                    return Task.CompletedTask;
                },
                async () => await Task.Yield(),

                () =>
                {
                    var rndThreadPriority = (ThreadPriority)s_threadRandom.Value.Next( (Int32)ThreadPriority.Highest );
                    Thread.CurrentThread.Priority = rndThreadPriority;

                    return Task.CompletedTask;
                }
            };
        }

        public static Task TryForceAsync()
        {
#if TEST_CONCURRENCY
            Int32 methodIndex = s_threadRandom.Value.Next( s_forcingConcurrencyErrorMethods.Length );
            Func<Task> rndMethodToForceError = s_forcingConcurrencyErrorMethods[ methodIndex ];

            return rndMethodToForceError();
#else
            return Task.CompletedTask;
#endif
        }

        private static Int32 RndSleepTime() =>
            s_threadRandom.Value.Next( MAX_SLEEP_TIME_IN_MS );
    }
}
