using System;
using System.Collections.Generic;
using System.Linq;

namespace DiscoveryServices.Common.Extensions
{
    public static class IDictionaryExtension
    {
        public static Boolean Equals<TKey, TValue>( this IDictionary<TKey, TValue> dict, IDictionary<TKey, TValue> anotherDict )
        {
            Boolean isEqual = dict.Count == anotherDict.Count;

            //if dictionaries have different count of elements, then
            //they aren't equal because they have different set of keys
            if ( isEqual )
            {
                foreach ( KeyValuePair<TKey, TValue> pair in anotherDict )
                {
                    isEqual = dict.Any( c => c.Key.Equals( pair.Key ) && c.Value.Equals( pair.Value ) );
                    if ( !isEqual )
                    {
                        break;
                    }
                }
            }

            return isEqual;
        }
    }
}
