using Serilog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LUC.Interfaces.Extensions
{
    public static class ConcurrentDictionaryExtension
    {
        public static void Add<TKey, TValue>( this ConcurrentDictionary<TKey, TValue> concurrentDictionary, TKey key, TValue value ) =>
            concurrentDictionary.AddOrUpdate( key, k => value, ( k, previousValue ) => value );

        public static Boolean TryRemove<TKey, TValue>( this ConcurrentDictionary<TKey, TValue> concurrentDictionary, TKey key ) =>
            concurrentDictionary.TryRemove( key, value: out _ );

        public static void RemoveRange<TKey, TValue>( this ConcurrentDictionary<TKey, TValue> concurrentDictionary, IEnumerable<TKey> keys )
        {
            while ( concurrentDictionary.Keys.AsParallel().Any( k => keys.AsParallel().Any( k2 => k2.Equals( k ) ) ) )
            {
                Parallel.ForEach( keys, key => concurrentDictionary.TryRemove( key ) );
            }
        }

        public static Int32 RemoveAll<TKey, TValue>( this ConcurrentDictionary<TKey, TValue> concurrentCollection, Func<KeyValuePair<TKey, TValue>, Boolean> predicate )
        {
            Int32 countDeletedItems = 0;
            try
            {
                Parallel.ForEach( concurrentCollection.AsParallel().Where( predicate ), item =>
                {
                    Boolean isRemoved = concurrentCollection.TryRemove( item.Key, value: out _ );
                    if ( isRemoved )
                    {
                        Interlocked.Increment( ref countDeletedItems );
                    }
                } );
            }
            catch ( AggregateException ex )
            {
                Log.Error( ex, messageTemplate: ex.Message );
            }

            return countDeletedItems;
        }
    }
}
