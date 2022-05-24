using System;
using System.Collections.Generic;

namespace DiscoveryServices.Common.Extensions
{
    class DictionaryEqualityComparer<TKey, TValue> : IEqualityComparer<KeyValuePair<TKey, TValue>>
    {
        public Boolean Equals( KeyValuePair<TKey, TValue> pair1, KeyValuePair<TKey, TValue> pair2 ) =>
            pair1.Key.Equals( pair2.Key ) && pair1.Value.Equals( pair2.Value );

        public Int32 GetHashCode( KeyValuePair<TKey, TValue> pair ) =>
            HashCode.Combine( pair.Key, pair.Value );
    }
}
