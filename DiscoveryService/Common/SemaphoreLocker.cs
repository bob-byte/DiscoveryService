using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Common
{
    public class SemaphoreLocker
    {
        private readonly SemaphoreSlim m_semaphoreSlim = new SemaphoreSlim( initialCount: 1, maxCount: 1 );

        public void Lock( Action procedure )
        {
            m_semaphoreSlim.Wait();

            try
            {
                procedure();
            }
            finally
            {
                m_semaphoreSlim.Release();
            }
        }

        public T Lock<T>( Func<T> func )
        {
            m_semaphoreSlim.Wait();

            try
            {
                T result = func();
                return result;
            }
            finally
            {
                m_semaphoreSlim.Release();
            }
        }

        public async Task LockAsync( Func<Task> procedure )
        {
            await m_semaphoreSlim.WaitAsync();

            try
            {
                await procedure();
            }
            finally
            {
                m_semaphoreSlim.Release();
            }
        }

        public async Task<T> LockAsync<T>( Func<Task<T>> func )
        {
            await m_semaphoreSlim.WaitAsync();

            try
            {
                T result = await func();
                return result;
            }
            finally
            {
                m_semaphoreSlim.Release();
            }
        }
    }
}
