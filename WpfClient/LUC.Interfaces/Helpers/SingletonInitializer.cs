using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.Interfaces.Helpers
{
    public static class SingletonInitializer
    {
        private static readonly Object s_lock = new Object();

        /// <summary>
        /// It initializes <paramref name="variable"/> with the <paramref name="value"/> using common lock
        /// </summary>
        /// <typeparam name="T">
        /// Type of <paramref name="variable"/>
        /// </typeparam>
        /// <param name="value">
        /// Value to initialize <paramref name="variable"/>
        /// </param>
        /// <param name="variable">
        /// Variable which is needed to initialize 
        /// </param>
        public static void ThreadSafeInit<T>( Func<T> value, ref T variable ) =>
            ThreadSafeInit( value, s_lock, ref variable );

        public static T Initialized<T>(Func<T> value, T currentValue)
        {
            T initialized = currentValue == null ? value() : currentValue;
            return initialized;
        }

        /// <summary>
        /// It initializes <paramref name="variable"/> with the <paramref name="value"/> using <paramref name="locker"/>
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
        public static void ThreadSafeInit<T>( Func<T> value, Object locker, ref T variable )
        {
            if ( EqualityComparer<T>.Default.Equals( variable, default ) )
            {
                lock ( locker )
                {
                    if ( EqualityComparer<T>.Default.Equals( variable, default ) )
                    {
                        variable = value();
                    }
                }
            }
        }
    }
}
