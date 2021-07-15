using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService
{
    public class SemaphoreLocker
    {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(initialCount: 1, maxCount: 1);

        public async Task LockAsync(Func<Task> func)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                await func();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public async Task<T> LockAsync<T>(Func<Task<T>> func)
        {
            await semaphoreSlim.WaitAsync();
            try
            {
                return await func();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }
    }
}
