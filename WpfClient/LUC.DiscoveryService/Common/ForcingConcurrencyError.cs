//#define TEST_CONCURRENCY

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    public sealed class ForcingConcurrencyError : IDisposable
    {
        private const Int32 MAX_SLEEP_TIME_IN_MS = 2000; //2 seconds

        private static readonly ThreadLocal<Random> s_threadRandom = new ThreadLocal<Random>( () => new Random() );

        private static readonly ConcurrentBag<Action> s_forcingConcurrencyErrorMethods;

        static ForcingConcurrencyError()
        {
            s_forcingConcurrencyErrorMethods = new ConcurrentBag<Action>
            {
                () =>
                {
                    Int32 rndSleepTime = RndSleepTime();
                    Thread.Sleep( rndSleepTime );
                },
                async () =>
                {
                    Int32 rndSleepTime = RndSleepTime();
                    await Task.Delay( rndSleepTime ).ConfigureAwait(continueOnCapturedContext: false);
                },

                () => Thread.Yield(),
                async () => await Task.Yield(),

                () =>
                {
                    var rndThreadPriority = (ThreadPriority)s_threadRandom.Value.Next( (Int32)ThreadPriority.Highest );

                    Thread.CurrentThread.Priority = rndThreadPriority;
                }
            };
        }

        private static Int32 RndSleepTime() =>
            s_threadRandom.Value.Next( MAX_SLEEP_TIME_IN_MS );

#pragma warning disable S1186 // Methods should not be empty
        public static void TryForce()
#pragma warning restore S1186 // Methods should not be empty
        {
#if TEST_CONCURRENCY
            Int32 methodIndex = t_random.Value.Next( s_forcingConcurrencyErrorMethods.Count );
            Action rndMethodToForceError = s_forcingConcurrencyErrorMethods.ToArray()[ methodIndex ];

            rndMethodToForceError();
#endif
        }

        public void Dispose() =>
            s_threadRandom.Dispose();
    }
}
