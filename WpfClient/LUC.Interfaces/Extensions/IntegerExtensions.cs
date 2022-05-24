using System;
using System.Collections.Generic;
using System.Linq;

namespace LUC.Interfaces.Extensions
{
    public static class IntegerExtensions
    {
        public static DateTime FromUnixTimeStampToDateTime( this Int64 unixTimeStamp )
        {
            if ( unixTimeStamp < 0 )
            {
                // TODO Delete
            }

            var dateTime = new DateTime( 1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc );
            Int32 unixTimestamp = (Int32)DateTime.UtcNow.Subtract( dateTime ).TotalSeconds;
            dateTime = dateTime.AddSeconds( unixTimestamp );
            return dateTime;
        }

        public static Int32 Mod( this Int32 a, Int32 b ) =>
            ( ( a % b ) + b ) % b;

        public static IEnumerable<Int32> Range( this Int32 n ) =>
            Enumerable.Range( start: 0, n );

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

        public static Int64 FromDateTimeToUnixTimeStamp( this DateTime dateTime )
        {
            Double result = ( dateTime - new DateTime( 1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc ) ).TotalSeconds;

            if ( result < 0 )
            {
                result = 0;
            }

            return (Int64)result;
        }
    }
}
