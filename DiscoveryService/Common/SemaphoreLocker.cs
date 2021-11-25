using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.DiscoveryServices.Common
{
    public class SemaphoreLocker
    {
        private readonly SemaphoreSlim m_semaphoreSlim = new SemaphoreSlim( initialCount: 1, maxCount: 1 );

        public void Lock( Action procedure )
        {
            Boolean isTaken = false;

            try
            {
                do
                {
                    try
                    {
                    }
                    finally
                    {
                        isTaken = m_semaphoreSlim.Wait( TimeSpan.FromSeconds( value: 1 ) );
                    }
                }
                while ( !isTaken );

                procedure();
            }
            finally
            {
                if ( isTaken )
                {
                    m_semaphoreSlim.Release();
                }
            }
        }

        public T Lock<T>( Func<T> func )
        {
            Boolean isTaken = false;

            try
            {
                do
                {
                    try
                    {
                    }
                    finally
                    {
                        isTaken = m_semaphoreSlim.Wait( TimeSpan.FromSeconds( 1 ) );
                    }
                }
                while ( !isTaken );

                T result = func();
                return result;
            }
            finally
            {
                if ( isTaken )
                {
                    m_semaphoreSlim.Release();
                }
            }
        }

        //https://stackoverflow.com/a/61806749/7889645"
        public async Task LockAsync( Func<Task> procedure )
        {
            Boolean isTaken = false;

            try
            {
                do
                {
                    try
                    {
                    }
                    finally
                    {
                        isTaken = await m_semaphoreSlim.WaitAsync( TimeSpan.FromSeconds( 1 ) );
                    }
                }
                while ( !isTaken );

                await procedure();
            }
            finally
            {
                if ( isTaken )
                {
                    m_semaphoreSlim.Release();
                }
            }
        }

        //https://stackoverflow.com/a/61806749/7889645"
        public async Task<T> LockAsync<T>( Func<Task<T>> func )
        {
            Boolean isTaken = false;

            try
            {
                do
                {
                    try
                    {
                    }
                    finally
                    {
                        isTaken = await m_semaphoreSlim.WaitAsync( TimeSpan.FromSeconds( 1 ) );
                    }
                }
                while ( !isTaken );

                T result = await func();
                return result;
            }
            finally
            {
                if ( isTaken )
                {
                    m_semaphoreSlim.Release();
                }
            }
        }
    }
}
