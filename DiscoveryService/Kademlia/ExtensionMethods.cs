using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LUC.DiscoveryServices.Kademlia
{
    public static class ExtensionMethods
    {
        public static void ForEach<T>( this IEnumerable<T> collection, Action<T> action )
        {
            foreach ( T item in collection )
            {
                action( item );
            }
        }

        /// <summary>
        /// ForEach with an index.
        /// </summary>
        public static void ForEachWithIndex<T>( this IEnumerable<T> collection, Action<T, Int32> action )
        {
            Int32 n = 0;

            foreach ( T item in collection )
            {
                action( item, n++ );
            }
        }

        /// <summary>
        /// Implements ForEach for non-generic enumerators.
        /// </summary>
        // Usage: Controls.ForEach<Control>(t=>t.DoSomething());
        public static void ForEach<T>( this IEnumerable collection, Action<T> action )
        {
            foreach ( T item in collection )
            {
                action( item );
            }
        }

        public static void ForEach( this Int32 n, Action action )
        {
            for ( Int32 i = 0; i < n; i++ )
            {
                action();
            }
        }

        public static void ForEach( this Int32 n, Action<Int32> action )
        {
            for ( Int32 i = 0; i < n; i++ )
            {
                action( i );
            }
        }

        public static IEnumerable<Int32> Range( this Int32 n ) => 
            Enumerable.Range( 0, n );

        public static void MoveToTail<T>( this List<T> list, T item, Predicate<T> pred )
        {
            Int32 idx = list.FindIndex( pred );
            list.RemoveAt( idx );
            list.Add( item );
        }

        public static void AddMaximum<T>( this List<T> list, T item, Int32 max )
        {
            list.Add( item );

            if ( list.Count > max )
            {
                list.RemoveAt( 0 );
            }
        }

        public static void AddDistinct<T>( this List<T> list, T item )
        {
            if ( !list.Contains( item ) )
            {
                list.Add( item );
            }
        }

        public static Boolean ContainsBy<T, TKey>( this List<T> list, T item, Func<T, TKey> keySelector )
        {
            TKey itemKey = keySelector( item );

            return list.Any( n => keySelector( n ).Equals( itemKey ) );
        }

        public static void AddDistinctBy<T, TKey>( this List<T> list, T item, Func<T, TKey> keySelector )
        {
            TKey itemKey = keySelector( item );

            // no items in the list must match the item.
            if ( list.None( q => keySelector( q ).Equals( itemKey ) ) )
            {
                list.Add( item );
            }
        }

        // TODO: Change the equalityComparer to a KeySelector for the these extension methods:
        public static void AddRangeDistinctBy<T>( this List<T> target, IEnumerable<T> src, Func<T, T, Boolean> equalityComparer )
        {
            src.ForEach( item =>
            {
                // no items in the list must match the item.
                if ( target.None( q => equalityComparer( q, item ) ) )
                {
                    target.Add( item );
                }
            } );
        }
            
        public static IEnumerable<T> ExceptBy<T, TKey>( this IEnumerable<T> src, T item, Func<T, TKey> keySelector )
        {
            TKey itemKey = keySelector( item );

            using ( IEnumerator<T> enumerator = src.GetEnumerator() )
            {
                while ( enumerator.MoveNext() )
                {
                    T current = enumerator.Current;

                    if ( !keySelector( current ).Equals( itemKey ) )
                    {
                        yield return current;
                    }
                }
            }
        }

        public static IEnumerable<T> ExceptBy<T, TKey>( this IEnumerable<T> src, IEnumerable<T> items, Func<T, TKey> keySelector )
        {
            using ( IEnumerator<T> enumerator = src.GetEnumerator() )
            {
                while ( enumerator.MoveNext() )
                {
                    T current = enumerator.Current;

                    if ( items.None( i => keySelector( current ).Equals( keySelector( i ) ) ) )
                    {
                        yield return current;
                    }
                }
            }
        }

        public static Boolean None<TSource>( this IEnumerable<TSource> source ) => 
            !source.Any();

        public static Boolean None<TSource>( this IEnumerable<TSource> source, Func<TSource, Boolean> predicate ) => 
            !source.Any( predicate );

        public static void RemoveRange<T>( this ICollection<T> target, ICollection<T> src ) => 
            src.ForEach( s => target.Remove( s ) );

        public static void RemoveRange<T>( this List<T> target, List<T> src, Func<T, T, Boolean> equalityComparer )
        {
            src.ForEach( s =>
            {
                Int32 idx = target.FindIndex( t => equalityComparer( t, s ) );
                if ( idx != -1 )
                {
                    target.RemoveAt( idx );
                }
            } );
        }

        public static Int32 Mod( this Int32 a, Int32 b ) => 
            ( ( a % b ) + b ) % b;

        public static T Second<T>( this List<T> items ) => 
            items[ 1 ];

        /// <summary>
        /// Little endian conversion of bytes to bits.
        /// </summary>
		public static IEnumerable<Boolean> Bits( this Byte[] bytes )
        {
            IEnumerable<Boolean> Bits( Byte b )
            {
                Byte shifter = 0x01;

                for ( Int32 i = 0; i < 8; i++ )
                {
                    yield return ( b & shifter ) != 0;
                    shifter <<= 1;
                }
            }

            return bytes.SelectMany( Bits );
        }

        /// <summary>
        /// Value cannot exceed max.
        /// </summary>
        public static Int32 Min( this Int32 a, Int32 max ) => 
            ( a > max ) ? max : a;

        /// <summary>
        /// Value cannot be less than min.
        /// </summary>
        public static Int32 Max( this Int32 a, Int32 min ) => 
            ( a < min ) ? min : a;

        public static T Next<T>( this IEnumerable<T> source )
        {
            using ( IEnumerator<T> enumerator = source.GetEnumerator() )
            {
                enumerator.MoveNext();

                return enumerator.Current;
            }
        }

        public static Boolean IsNext<T>( this IEnumerable<T> source, Func<T, Boolean> predicate )
        {
            using ( IEnumerator<T> enumerator = source.GetEnumerator() )
            {
                enumerator.MoveNext();
                return predicate( enumerator.Current );
            }
        }

        /// <summary>
        /// Append a 0 to the byte array so that when converting to a BigInteger, the value remains positive.
        /// </summary>
        public static Byte[] Append0( this Byte[] b ) => 
            b.Concat( new Byte[] { 0 } ).ToArray();

        public static Boolean ApproximatelyEquals( this Double d, Double val, Double range ) =>
            ( d >= val - range ) && ( d <= val + range );

        // Welford's method: https://mathoverflow.net/questions/70345/numerically-most-robust-way-to-compute-sum-of-products-standard-deviation-in-f
        // From: https://stackoverflow.com/questions/2253874/standard-deviation-in-linq
        public static Double StdDev( this IEnumerable<Double> values )
        {
            Double mean = 0.0;
            Double sum = 0.0;
            Double stdDev = 0.0;
            Int32 n = 0;

            foreach ( Double val in values )
            {
                n++;

                Double delta = val - mean;
                mean += delta / n;

                sum += delta * ( val - mean );
            }

            if ( 1 < n )
            {
                stdDev = Math.Sqrt( sum / ( n - 1 ) );
            }

            return stdDev;
        }

        public static Byte[] ToUtf8( this String str ) => 
            Encoding.UTF8.GetBytes( str );

        public static Int32 ToInt32( this String str ) => 
            Convert.ToInt32( str );


        public static IEnumerable<T> WhereAll<T>( this IEnumerable<T> a, IEnumerable<T> b, Func<T, T, Boolean> comparator )
        {
            using ( IEnumerator<T> aenum = a.GetEnumerator() )
            {
                while ( aenum.MoveNext() )
                {
                    T aa = aenum.Current;

                    if ( b.All( bb => comparator( aa, bb ) ) )
                    {
                        yield return aa;
                    }
                }
            }
        }
    }
}
