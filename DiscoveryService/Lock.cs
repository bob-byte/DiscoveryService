using System;

namespace LUC.DiscoveryService
{
    /// <summary>
    /// Contrains locks and method for initialization variables
    /// </summary>
    static class Lock
    {
        internal static Object LockService { get; }

        //if we don't use static constructor we will not actually know when fields are inizialized
        static Lock()
        {
            LockService = new Object();
        }

        /// <summary>
        /// It initialize <paramref name="variable"/> with the <paramref name="value"/> using <paramref name="locker"/>
        /// </summary>
        /// <typeparam name="T">
        /// Type of property
        /// </typeparam>
        /// <param name="locker">
        /// Lock to initialize
        /// </param>
        /// <param name="value">
        /// Value to initialize <paramref name="variable"/>
        /// </param>
        /// <param name="variable">
        /// Variable which is needed to initialize 
        /// </param>
        internal static void InitWithLock<T>(Object locker, T value, ref T variable)
        {
            if (variable == null)
            {
                lock (locker)
                {
                    if (variable == null)
                    {
                        variable = value;
                    }
                }
            }
        }
    }
}
